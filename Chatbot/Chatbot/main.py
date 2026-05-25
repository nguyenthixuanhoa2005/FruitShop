from fastapi import FastAPI, Request, HTTPException
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles
from fastapi.middleware.cors import CORSMiddleware
import os
import re
import random
import difflib
import uuid
import json
import unicodedata
from urllib import request as urlrequest
from urllib.error import URLError, HTTPError
from datetime import datetime
import pyodbc
from config_private import SQL_CONN_STR, GEMINI_API_KEY, DIFY_API_KEY

app = FastAPI()
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)
app.mount("/static", StaticFiles(directory="static"), name="static")

DIFY_API_URL = "https://api.dify.ai/v1"
BASE_DIR = os.path.dirname(__file__)
PRODUCTS_CACHE = []
PRODUCTS_BY_ID = {}
CONVERSATION_STATE = {}
FEEDBACK_LOG = []
VECTOR_DB_DIR = os.path.join(BASE_DIR, "vector_storage")
VECTOR_COLLECTION_NAME = "fruit_shop_products"
CHROMA_CLIENT = None
VECTOR_COLLECTION = None
VECTOR_READY = False
VECTOR_LAST_ERROR = ""
GEMINI_MODEL = "gemini-1.5-flash"
FRUIT_SHOP_CONTACT = (
    "Thông tin liên hệ trực tiếp FRUIT_SHOP: "
    "SĐT/Zalo 0333890139 | "
    "Địa chỉ: 127 Lê Quang Định, Phường 14, Quận Bình Thạnh, TP.HCM | "
    "Zalo: https://zalo.me/0333890139"
)

PRODUCT_TYPES = {
    "trai_cay_kg": "trái cây bán theo kg",
    "gio_qua": "giỏ quà",
    "hop_qua": "hộp quà",
}

TEXT_TYPE_MARKERS = {
    "hop_qua": ("hop qua", "gift box", "box qua"),
    "gio_qua": ("gio qua", "gio hoa qua", "hoa qua", "qua tang", "qua bieu", "bieu tang", "lam qua"),
    "trai_cay_kg": ("kg", "kilo", "can", "theo kg", "theo can"),
}

CONSUMPTION_MARKERS = ("de an", "an lien", "an ngay", "an vat", "trang mieng", "ep nuoc", "lam nuoc ep")

GREETING_WORDS = {
    "hi", "hello", "helo", "hey", "yo", "chao", "xin", "alo", "ê", "e", "ok", "oke", "hii", "xin chào", 
    "chào bạn", "chào"
}
PRODUCT_HINT_WORDS = {
    "mua", "bán", "trái", "cây", "táo", "cam", "xòai", "nho", "dưa", "chuối", "giá", "rẻ",
    "ngon", "tươi", "giảm", "khuyến", "mãi", "đề", "xuất", "gợi", "ý", "san", "pham", "tim",
    "muon", "salad", "ep", "nuoc", "qua", "kg", "hop", "sale"
}

SALE_MARKERS = ("sale", "giam gia", "khuyen mai", "uu dai", "deal", "flash sale")
CONSULTING_MARKERS = (
    "mo ta", "chi tiet", "thong tin", "dac diem", "huong vi", "xuat xu", "bao quan",
    "thanh phan", "tu van", "goi y", "nen mua", "co gi", "la gi",
)

FOLLOWUP_DETAIL_MARKERS = (
    "them", "them di", "ro hon", "cu the hon", "chi tiet hon", "mo ta hon", "thong tin them",
)

CONTEXT_PRODUCT_REFERENCE_MARKERS = (
    "san pham nay", "mon nay", "loai nay", "gio nay", "hop nay", "cai nay", "nay",
    "co nhung gi", "gom gi", "thanh phan gi", "mo ta cho toi", "mo ta cho minh",
)

COMPARE_MARKERS = (
    "so sanh", "khac nhau", "tot hon", "nen chon", "chon loai nao", "doi chieu",
)

INFO_MARKERS = (
    "mo ta", "chi tiet", "thong tin", "thanh phan", "xuat xu", "bao quan", "dac diem",
)

ADVICE_MARKERS = (
    "tu van", "goi y", "nen mua", "de xuat", "chon gi", "phu hop",
)

AVAILABILITY_MARKERS = (
    "co khong", "con khong", "co ban khong", "co san khong", "co hang khong",
    "ban khong", "dang ban khong", "cua hang co", "shop co", "co loai nao",
)

BUY_DECISION_MARKERS = (
    "nen mua khong", "co nen mua", "mua duoc khong", "chot duoc khong",
    "co dang mua", "co dang tien", "co on khong",
)

BUSINESS_SALES_MARKERS = (
    "tu van ban hang", "tu van cho khach", "kich ban ban hang", "kich ban chot don",
    "chot don", "upsell", "cross sell", "ban kem", "ban cheo", "noi voi khach",
)

QUERY_REWRITE_MAP = {
    "khuyen mai": "sale",
    "giam gia": "sale",
    "uu dai": "sale",
    "deals": "sale",
    "qua tang": "gio qua",
    "gio qua": "gio qua",
    "hop qua": "hop qua",
}

QUERY_NORMALIZATION_MAP = {
    "ko": "khong",
    "k": "khong",
    "hok": "khong",
    "hem": "khong",
    "dc": "duoc",
    "thfi": "thi",
    "thui": "thoi",
    "thik": "thich",
    "mún": "muon",
    "mun": "muon",
    "sp": "san pham",
    "mk": "minh",
    "mik": "minh",
    "z": "vay",
    "j": "gi",
    "baonhieu": "bao nhieu",
    "bnhieu": "bao nhieu",
}

AUTOCORRECT_IGNORE_TOKENS = {"kg", "ml", "l", "cm"}

CHEAP_PHRASES = {
    "gia re",
    "re hon",
    "gia thap",
    "thap hon",
    "gia mem",
    "binh dan",
    "it tien",
}

CHEAP_STOP_WORDS = {
    "gia", "re", "hon", "co", "ko", "khong", "k", "nhe", "nha", "khong", "nao", "ha", "a", "oi", "di", "qua"
}

QUERY_STOP_WORDS = {
    "gia", "tien", "loai", "trai", "cay", "san", "pham", "cho", "minh", "toi", "em", "anh", "chi",
    "ban", "nhe", "nha", "voi", "di", "nao", "a", "oi", "ko", "khong", "k", "co", "khong", "tim",
    "goi", "y", "de", "xuat", "hon", "tren", "duoi", "tu", "den", "khoang", "tam", "muc", "qua",
    "thu", "nhat", "hai", "ba", "bon", "nam", "sau", "bay", "tam", "chin", "muoi", "dau", "tien"
}

GIFT_MARKERS = ("gio qua", "hop qua", "qua tang", "lam qua", "qua bieu", "bieu tang")

PURPOSE_MARKERS = {
    "an_lien": ("an lien", "an ngay", "an vat", "trang mieng"),
    "ep_nuoc": ("ep nuoc", "lam nuoc ep", "smoothie"),
    "lam_qua": ("lam qua", "qua tang", "qua bieu", "tang sep", "tang doi tac", "bien tang"),
}

RECIPIENT_MARKERS = {
    "gia_dinh": ("gia dinh", "ca nha", "ba me", "bo me", "ong ba", "tre em"),
    "doi_tac": ("doi tac", "khach hang", "sep", "cong ty"),
    "nguoi_yeu": ("nguoi yeu", "ban gai", "ban trai", "vo", "chong"),
    "ban_be": ("ban be", "dong nghiep"),
}

FRUIT_ENTITY_TOKENS = {
    "tao", "cam", "xoai", "nho", "dua", "chuoi", "le", "kiwi", "man", "vai", "oi", "buoi",
}

FRUIT_PHRASE_MARKERS = {
    "sau rieng",
    "dua luoi",
    "viet quat",
    "thanh long",
    "chom chom",
    "mang cut",
    "vu sua",
    "quyt hong",
    "cam sanh",
}


def strip_accents(text: str) -> str:
    normalized = unicodedata.normalize("NFKD", str(text or ""))
    return "".join(ch for ch in normalized if not unicodedata.combining(ch)).lower()


def svg_placeholder(title: str) -> str:
    safe_title = str(title or "Sản phẩm")[:24]
    safe_title = safe_title.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
    svg = f"""
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 800 600">
        <defs>
            <linearGradient id="g" x1="0" x2="1" y1="0" y2="1">
                <stop offset="0%" stop-color="#22c55e"/>
                <stop offset="100%" stop-color="#f97316"/>
            </linearGradient>
        </defs>
        <rect width="800" height="600" fill="#0f172a"/>
        <circle cx="140" cy="110" r="150" fill="rgba(34,197,94,0.24)"/>
        <circle cx="670" cy="120" r="170" fill="rgba(249,115,22,0.22)"/>
        <rect x="65" y="92" width="670" height="416" rx="44" fill="url(#g)" opacity="0.94"/>
        <text x="400" y="305" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" font-size="44" font-weight="700" fill="#ffffff">{safe_title}</text>
        <text x="400" y="372" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" font-size="24" fill="#eef2ff">Ảnh minh họa sản phẩm</text>
    </svg>
    """
    return "data:image/svg+xml;charset=UTF-8," + svg.replace("\n", "").replace("  ", "")


def compact_text(text: str, limit: int = 160) -> str:
    cleaned = re.sub(r"\s+", " ", str(text or "")).strip()
    if len(cleaned) <= limit:
        return cleaned
    return cleaned[: max(0, limit - 3)].rstrip() + "..."


def summarize_product_reason(product: dict) -> str:
    description = strip_accents(product.get("description") or "")
    product_type = product.get("product_type") or "trai_cay_kg"

    reason_map = [
        ("qua tang", "phù hợp để làm quà tặng, nhìn gọn và dễ biếu"),
        ("qua bieu", "hợp cho mục đích biếu tặng, tạo cảm giác lịch sự"),
        ("nhap khau", "hợp nếu bạn muốn chọn món trông cao cấp hơn"),
        ("cap cao", "phù hợp khi muốn ưu tiên sự chỉn chu và sang hơn"),
        ("tuoi", "thích hợp nếu bạn ưu tiên độ tươi và ăn ngay"),
        ("ngot", "hợp với người thích vị ngọt, dễ ăn"),
        ("gion", "hợp khi muốn cảm giác giòn, dễ ăn và ít ngấy"),
        ("an lien", "phù hợp để ăn liền, tiện dùng ngay"),
        ("ep nuoc", "phù hợp nếu bạn muốn ép nước hoặc làm sinh tố"),
        ("salad", "hợp để làm salad hoặc mix nhiều loại trái cây"),
    ]

    for marker, reason in reason_map:
        if marker in description:
            return reason

    default_reason = {
        "gio_qua": "phù hợp nếu bạn đang cần một lựa chọn gọn để biếu tặng",
        "hop_qua": "phù hợp nếu bạn muốn một món quà chỉn chu và dễ đem tặng",
        "trai_cay_kg": "phù hợp nếu bạn muốn mua theo nhu cầu ăn hàng ngày hoặc dùng linh hoạt",
    }.get(product_type, "phù hợp với nhu cầu mua trái cây thông thường")

    return default_reason


def format_product_advice_line(product: dict) -> str:
    name = product.get("name", "Sản phẩm")
    description = compact_text(product.get("description") or "Chưa có mô tả chi tiết cho sản phẩm này.", 120)
    final_price = float(product.get("final_price") or 0)
    old_price = product.get("original_price")
    old_price_text = f" (giá gốc {old_price:,.0f}đ)" if old_price else ""
    reason = summarize_product_reason(product)
    unit_label = " /kg" if product.get("product_type") == "trai_cay_kg" else ""
    return f"{name}: {description} Giá {final_price:,.0f}đ{unit_label}{old_price_text}. Vì sao nên chọn: {reason}."


def tokenize(text: str):
    cleaned = strip_accents(text)
    return [tok for tok in re.findall(r"[a-z0-9]+", cleaned) if tok]


def build_query_vocabulary():
    vocab = set()

    marker_groups = [
        PRODUCT_HINT_WORDS,
        SALE_MARKERS,
        CONSULTING_MARKERS,
        FOLLOWUP_DETAIL_MARKERS,
        CONTEXT_PRODUCT_REFERENCE_MARKERS,
        COMPARE_MARKERS,
        INFO_MARKERS,
        ADVICE_MARKERS,
        AVAILABILITY_MARKERS,
        BUY_DECISION_MARKERS,
        BUSINESS_SALES_MARKERS,
        GIFT_MARKERS,
        CHEAP_PHRASES,
    ]
    for group in marker_groups:
        for item in group:
            vocab.update(tokenize(item))

    for value in QUERY_REWRITE_MAP.values():
        vocab.update(tokenize(value))
    for value in QUERY_NORMALIZATION_MAP.values():
        vocab.update(tokenize(value))

    vocab.update(FRUIT_ENTITY_TOKENS)
    vocab.update(tokenize("mo ta chi tiet san pham nen mua khong co khong con khong thu nhat thu hai thu ba"))

    for product in PRODUCTS_CACHE[:600]:
        vocab.update(tokenize(product.get("name") or ""))
        vocab.update(tokenize(product.get("unit") or ""))

    return {token for token in vocab if token}


def normalize_user_query(user_query: str) -> str:
    normalized = strip_accents(user_query)

    for source, target in QUERY_NORMALIZATION_MAP.items():
        normalized = re.sub(rf"\b{re.escape(source)}\b", target, normalized)

    tokens = [tok for tok in re.findall(r"[a-z0-9]+", normalized) if tok]
    if not tokens:
        return ""

    vocabulary = build_query_vocabulary()
    vocabulary_list = list(vocabulary)

    corrected_tokens = []
    for token in tokens:
        if (
            token in vocabulary
            or token in AUTOCORRECT_IGNORE_TOKENS
            or token.isdigit()
            or len(token) <= 2
        ):
            corrected_tokens.append(token)
            continue

        match = difflib.get_close_matches(token, vocabulary_list, n=1, cutoff=0.84)
        corrected_tokens.append(match[0] if match else token)

    return " ".join(corrected_tokens)


def normalize_intent_text(text: str) -> str:
    normalized = normalize_user_query(text)
    return normalized or strip_accents(text)


def has_gift_marker(text: str) -> bool:
    normalized = strip_accents(text)
    return any(marker in normalized for marker in GIFT_MARKERS)


def _contains_non_negated_marker(normalized_text: str, markers) -> bool:
    for marker in markers:
        if marker not in normalized_text:
            continue

        negation_patterns = (
            f"khong phai {marker}",
            f"ko phai {marker}",
            f"k phai {marker}",
            f"khong phai la {marker}",
            f"ko phai la {marker}",
            f"khong muon {marker}",
            f"ko muon {marker}",
            f"k muon {marker}",
            f"khong can {marker}",
            f"ko can {marker}",
        )
        if any(pattern in normalized_text for pattern in negation_patterns):
            continue
        return True
    return False


def is_negated_gift_request(text: str) -> bool:
    normalized = normalize_intent_text(text)
    negated_phrases = (
        "khong phai gio hoa qua", "ko phai gio hoa qua", "k phai gio hoa qua",
        "khong phai gio qua", "ko phai gio qua", "k phai gio qua",
        "khong phai hop qua", "ko phai hop qua", "k phai hop qua",
        "khong muon gio hoa qua", "ko muon gio hoa qua", "k muon gio hoa qua",
        "khong muon gio qua", "ko muon gio qua", "k muon gio qua",
        "khong muon hop qua", "ko muon hop qua", "k muon hop qua",
    )
    return any(phrase in normalized for phrase in negated_phrases)


def detect_product_type_from_text(text: str):
    normalized = normalize_intent_text(text)

    # Negated gift requests should force fresh fruit flow.
    if is_negated_gift_request(text):
        return "trai_cay_kg"

    # Explicit basket/bouquet language should always map to gift basket first.
    if _contains_non_negated_marker(normalized, ("gio hoa qua", "gio qua", "gio cua")):
        return "gio_qua"

    if any(marker in normalized for marker in FRUIT_PHRASE_MARKERS):
        return "trai_cay_kg"

    # Prioritize gift box before gift basket to avoid generic "quà" collisions.
    if _contains_non_negated_marker(normalized, TEXT_TYPE_MARKERS["hop_qua"]):
        return "hop_qua"
    if _contains_non_negated_marker(normalized, TEXT_TYPE_MARKERS["gio_qua"]):
        return "gio_qua"
    if any(marker in normalized for marker in TEXT_TYPE_MARKERS["trai_cay_kg"]):
        return "trai_cay_kg"

    # Queries like "mua it cam de an" should prefer fresh fruit instead of gift baskets.
    tokens = set(tokenize(text))
    has_fruit_name = bool(tokens & FRUIT_ENTITY_TOKENS)
    has_consumption_signal = any(marker in normalized for marker in CONSUMPTION_MARKERS)
    if has_fruit_name and has_consumption_signal and not _contains_non_negated_marker(normalized, TEXT_TYPE_MARKERS["gio_qua"]):
        return "trai_cay_kg"

    return None


def is_fresh_fruit_request(text: str) -> bool:
    normalized = normalize_intent_text(text)
    tokens = set(tokenize(text))
    has_fruit_name = bool(tokens & FRUIT_ENTITY_TOKENS) or any(marker in normalized for marker in FRUIT_PHRASE_MARKERS)

    negates_gift = is_negated_gift_request(text)
    has_consumption_signal = any(marker in normalized for marker in CONSUMPTION_MARKERS)

    return has_fruit_name and (has_consumption_signal or negates_gift)


def is_explicit_fruit_query(text: str) -> bool:
    normalized = normalize_intent_text(text)
    tokens = set(tokenize(text))
    has_fruit_name = bool(tokens & FRUIT_ENTITY_TOKENS) or any(marker in normalized for marker in FRUIT_PHRASE_MARKERS)
    has_gift_signal = _contains_non_negated_marker(normalized, TEXT_TYPE_MARKERS["gio_qua"]) or _contains_non_negated_marker(normalized, TEXT_TYPE_MARKERS["hop_qua"])
    return has_fruit_name and not has_gift_signal


def infer_product_type_from_blob(search_blob: str, unit: str = ""):
    blob = f"{search_blob} {strip_accents(unit)}"
    if "hop qua" in blob or "gift box" in blob:
        return "hop_qua"
    if "gio qua" in blob or "qua tang" in blob or "qua bieu" in blob:
        return "gio_qua"
    return "trai_cay_kg"


def product_type_label(product_type: str):
    return PRODUCT_TYPES.get(product_type, "sản phẩm")


def is_matching_product_type(product: dict, forced_type: str = None):
    if not forced_type:
        return True
    return product.get("product_type") == forced_type


def token_matches_blob(token: str, blob: str) -> bool:
    if token == "qua_tang":
        return has_gift_marker(blob)
    # Match whole tokens only to avoid substring noise like "cam" in unrelated words.
    return bool(re.search(rf"\b{re.escape(token)}\b", blob))


def is_sale_query(user_query: str) -> bool:
    normalized = normalize_intent_text(user_query)
    return any(marker in normalized for marker in SALE_MARKERS)


def is_product_consulting_query(user_query: str) -> bool:
    normalized = normalize_intent_text(user_query)
    return any(marker in normalized for marker in CONSULTING_MARKERS)


def is_availability_query(user_query: str) -> bool:
    normalized = normalize_intent_text(user_query)
    if any(marker in normalized for marker in ("gia re", "re hon", "thap hon", "so sanh", "nen chon", "bao nhieu", "gia bao")):
        return False
    return any(marker in normalized for marker in AVAILABILITY_MARKERS)


def is_buy_decision_query(user_query: str) -> bool:
    normalized = normalize_intent_text(user_query)
    return any(marker in normalized for marker in BUY_DECISION_MARKERS)


def is_deeper_product_followup_query(user_query: str) -> bool:
    normalized = normalize_intent_text(user_query)
    deeper_markers = (
        "ngon khong", "co ngon", "chat luong", "tuoi khong", "vi sao",
        "ky hon", "chi tiet hon", "tu van ky", "uu diem", "nhuoc diem",
        "bao quan", "an sao", "dung sao", "phu hop voi", "co nen",
    )
    return any(marker in normalized for marker in deeper_markers)


def build_deeper_product_followup_reply(user_query: str, product: dict) -> str:
    normalized = normalize_intent_text(user_query)
    name = product.get("name", "Sản phẩm này")
    description = compact_text(product.get("description") or "Đây là sản phẩm có mô tả tốt trong hệ thống.", 140)
    reason = summarize_product_reason(product)
    price = float(product.get("final_price") or 0)
    unit_label = "/kg" if product.get("product_type") == "trai_cay_kg" else ""

    lines = [
        f"Nếu xét theo nhu cầu bạn đang hỏi thì {name} là lựa chọn ổn đó.",
        f"Theo mô tả hiện có: {description}",
        f"Giá đang là {price:,.0f}đ{unit_label}, và điểm đáng cân nhắc là {reason}.",
    ]

    if "ngon" in normalized:
        lines.append("Nếu bạn ưu tiên vị dễ ăn hằng ngày thì món này hợp, còn muốn vị thật đậm thì mình có thể gợi ý thêm một lựa chọn khác để so nhanh.")
    elif "bao quan" in normalized:
        lines.append("Mẹo nhanh: nên giữ mát, khô thoáng và tránh đè nén để giữ chất lượng ổn định hơn.")
    elif "co nen" in normalized or "vi sao" in normalized:
        lines.append("Chốt lại: trong cùng tầm giá thì đây là lựa chọn đáng mua, đặc biệt nếu bạn muốn dùng linh hoạt mỗi ngày.")
    else:
        lines.append("Nếu bạn muốn, mình có thể so luôn món này với 1-2 sản phẩm cùng nhóm để bạn chốt nhanh hơn.")

    return " ".join(lines)


def build_availability_reply(product: dict = None, forced_type: str = None) -> str:
    if product:
        stock = int(product.get("stock_quantity") or 0)
        if stock > 0:
            return (
                f"Có nha, {product.get('name', 'sản phẩm này')} đang có hàng và rất hợp gu bạn đó. "
                "Nếu bạn muốn, mình chốt nhanh giúp bạn luôn."
            )
        return (
            f"Hiện {product.get('name', 'sản phẩm này')} tạm hết hàng rồi nè. "
            "Mình có thể gợi ý mẫu tương đương để bạn chọn ngay nhé."
        )

    type_label = product_type_label(forced_type) if forced_type else "trái cây"
    return f"Có nha, bên mình vẫn có {type_label}. Bạn muốn mình gửi vài lựa chọn hợp gu bạn luôn không?"


def build_buy_decision_reply(product: dict) -> str:
    name = product.get("name", "Sản phẩm này")
    price = float(product.get("final_price") or 0)
    reason = summarize_product_reason(product)
    return (
        f"Có nha, món {name} rất đáng mua đó. "
        f"Tầm giá {price:,.0f}đ khá hợp lý với chất lượng hiện có, và {reason}. "
        "Gu chọn của bạn ổn lắm, chốt món này là đẹp."
    )


def is_followup_detail_request(user_query: str) -> bool:
    normalized = normalize_intent_text(user_query)
    return any(marker in normalized for marker in FOLLOWUP_DETAIL_MARKERS)


def is_contextual_product_followup(user_query: str) -> bool:
    normalized = normalize_intent_text(user_query)
    detail_signals = (
        any(marker in normalized for marker in FOLLOWUP_DETAIL_MARKERS)
        or any(marker in normalized for marker in INFO_MARKERS)
        or any(marker in normalized for marker in CONTEXT_PRODUCT_REFERENCE_MARKERS)
    )
    return detail_signals


def parse_requested_rank(user_query: str):
    normalized = f" {normalize_intent_text(user_query)} "
    rank_markers = [
        (1, (" thu nhat ", " dau tien ", " thu 1 ", " so 1 ")),
        (2, (" thu hai ", " thu 2 ", " so 2 ")),
        (3, (" thu ba ", " thu 3 ", " so 3 ")),
    ]
    for rank, markers in rank_markers:
        if any(marker in normalized for marker in markers):
            return rank

    tokens = tokenize(user_query)
    if len(tokens) <= 5 and (" o giua " in normalized or " o giua day " in normalized or " giua " in normalized):
        return 2

    numeric_rank = re.search(r"\b(?:thu|so)\s*([1-3])\b", normalized)
    if numeric_rank:
        try:
            return int(numeric_rank.group(1))
        except Exception:
            return None
    return None


def resolve_ranked_visible_product(user_query: str, previous_results=None):
    previous_results = previous_results or []
    if not previous_results:
        return None

    normalized = f" {normalize_intent_text(user_query)} "
    visible_rank_markers = [
        (1, (" san pham dau ", " san pham dau tien ", " san pham thu nhat ", " san pham so 1 ", " san pham 1 ")),
        (2, (" san pham thu hai ", " san pham so 2 ", " san pham 2 ")),
        (3, (" san pham thu ba ", " san pham so 3 ", " san pham 3 ")),
    ]

    for rank, markers in visible_rank_markers:
        if any(marker in normalized for marker in markers):
            rank_index = max(0, rank - 1)
            return previous_results[min(rank_index, len(previous_results) - 1)]

    if any(phrase in normalized for phrase in (" san pham hien thi ", " san pham dang hien thi ", " danh sach nay ", " 1 trong 3 ", " trong 3 san pham ")):
        requested_rank = parse_requested_rank(user_query)
        rank_index = max(0, (requested_rank or 1) - 1)
        return previous_results[min(rank_index, len(previous_results) - 1)]

    return None


def resolve_referenced_product(user_query: str, previous_results=None, preferred_type: str = None):
    normalized = strip_accents(user_query)
    previous_results = previous_results or []
    query_tokens = tokenize(normalized)
    query_fruit = extract_primary_fruit_token(user_query)

    requested_rank = parse_requested_rank(user_query)
    if requested_rank and previous_results:
        rank_index = max(0, requested_rank - 1)
        ranked_item = previous_results[min(rank_index, len(previous_results) - 1)]
        if not preferred_type or ranked_item.get("product_type") in {None, preferred_type}:
            return ranked_item

    named_candidates = []
    for product in previous_results:
        if preferred_type and product.get("product_type") not in {None, preferred_type}:
            continue
        score = score_product_against_query(product, normalized, query_tokens=query_tokens, query_fruit=query_fruit)
        if score > 0:
            named_candidates.append((score, -float(product.get("final_price") or 0), product))

    if named_candidates:
        named_candidates.sort(key=lambda item: (item[0], item[1]), reverse=True)
        return named_candidates[0][2]

    catalog_candidates = []
    for product in PRODUCTS_CACHE:
        if preferred_type and product.get("product_type") != preferred_type:
            continue
        score = score_product_against_query(product, normalized, query_tokens=query_tokens, query_fruit=query_fruit)
        if score > 0:
            catalog_candidates.append((score, -float(product.get("final_price") or 0), product))

    if catalog_candidates:
        catalog_candidates.sort(key=lambda item: (item[0], item[1]), reverse=True)
        return catalog_candidates[0][2]

    return None


def is_on_sale_product(product: dict) -> bool:
    original = product.get("original_price")
    final = float(product.get("final_price") or 0)
    if original is None:
        return False
    return float(original) > final


def to_product_response(product: dict):
    image_url = product.get("image_url")
    if image_url:
        if not (image_url.startswith("http") or image_url.startswith("/") or image_url.startswith("data:")):
            image_url = f"/Upload/Products/{image_url}"
    else:
        image_url = svg_placeholder(product["name"])

    return {
        "id": product["id"],
        "name": product["name"],
        "description": product["description"],
        "final_price": product["final_price"],
        "original_price": product.get("original_price"),
        "rating_count": product.get("rating_count"),
        "review_count": product.get("review_count"),
        "ratings_count": product.get("ratings_count"),
        "average_rating": product.get("average_rating"),
        "avg_rating": product.get("avg_rating"),
        "rating": product.get("rating"),
        "stock_quantity": product["stock_quantity"],
        "unit": product["unit"],
        "product_type": product.get("product_type"),
        "related_product_ids": product.get("related_product_ids") or [],
        "image_url": image_url,
        "buy_url": f"/Products/Details/{product['id']}",
    }


def init_vector_store():
    global CHROMA_CLIENT, VECTOR_COLLECTION, VECTOR_READY, VECTOR_LAST_ERROR
    try:
        import chromadb

        CHROMA_CLIENT = chromadb.PersistentClient(path=VECTOR_DB_DIR)
        VECTOR_COLLECTION = CHROMA_CLIENT.get_or_create_collection(
            name=VECTOR_COLLECTION_NAME,
            metadata={"hnsw:space": "cosine"},
        )
        VECTOR_READY = True
        VECTOR_LAST_ERROR = ""
    except Exception as exc:
        CHROMA_CLIENT = None
        VECTOR_COLLECTION = None
        VECTOR_READY = False
        VECTOR_LAST_ERROR = str(exc)


def sync_vector_index(products):
    global VECTOR_LAST_ERROR
    if not VECTOR_READY or VECTOR_COLLECTION is None:
        return

    try:
        ids = []
        documents = []
        metadatas = []
        for product in products:
            pid = str(product["id"])
            ptype = product.get("product_type") or "trai_cay_kg"
            ids.append(pid)
            documents.append(
                f"{product['name']}. {product.get('description') or ''}. "
                f"Đơn vị: {product.get('unit') or ''}. Loại: {product_type_label(ptype)}"
            )
            metadatas.append(
                {
                    "product_id": int(product["id"]),
                    "product_type": ptype,
                    "on_sale": bool(is_on_sale_product(product)),
                }
            )

        if ids:
            VECTOR_COLLECTION.upsert(ids=ids, documents=documents, metadatas=metadatas)
    except Exception as exc:
        VECTOR_LAST_ERROR = str(exc)


def vector_retrieve_scores(
    rewritten_query: str,
    forced_type: str = None,
    sale_only: bool = False,
    min_price: float = None,
    max_price: float = None,
    top_k: int = 10,
):
    global VECTOR_LAST_ERROR
    if not VECTOR_READY or VECTOR_COLLECTION is None:
        return {}

    scores = {}
    try:
        query_count = max(15, top_k * 5)
        result = VECTOR_COLLECTION.query(
            query_texts=[rewritten_query],
            n_results=query_count,
            include=["distances", "metadatas"],
        )

        ids = (result.get("ids") or [[]])[0]
        distances = (result.get("distances") or [[]])[0]
        metadatas = (result.get("metadatas") or [[]])[0]

        for idx, pid_str in enumerate(ids):
            metadata = metadatas[idx] if idx < len(metadatas) else {}
            pid = metadata.get("product_id")
            if pid is None:
                try:
                    pid = int(pid_str)
                except Exception:
                    continue

            product = PRODUCTS_BY_ID.get(int(pid))
            if not product:
                continue
            if not is_matching_product_type(product, forced_type):
                continue
            if sale_only and not is_on_sale_product(product):
                continue

            price = float(product.get("final_price") or 0)
            if min_price is not None and price < min_price:
                continue
            if max_price is not None and price > max_price:
                continue

            distance = float(distances[idx] if idx < len(distances) else 1.0)
            sim = 1.0 / (1.0 + max(0.0, distance))
            scores[int(pid)] = max(scores.get(int(pid), 0.0), sim)
    except Exception as exc:
        VECTOR_LAST_ERROR = str(exc)
        return {}

    return scores


NORMALIZED_PRODUCT_HINT_WORDS = {strip_accents(word) for word in PRODUCT_HINT_WORDS}


def classify_intent(user_query: str) -> str:
    normalized_query = normalize_intent_text(user_query)
    tokens = tokenize(normalized_query)
    if not tokens:
        return "empty"

    token_set = set(tokens)
    has_price = any(char.isdigit() for char in user_query)
    has_product_hint = bool(token_set & NORMALIZED_PRODUCT_HINT_WORDS)
    has_explicit_product_name = len(token_set - QUERY_STOP_WORDS) >= 2
    has_business_sales_signal = is_business_sales_query(user_query)

    if token_set <= GREETING_WORDS and len(tokens) <= 3:
        return "greeting"

    if has_business_sales_signal:
        return "chat"

    # Only route to shopping when there are explicit shopping signals.
    if has_product_hint or has_price or (is_product_consulting_query(normalized_query) and has_explicit_product_name):
        return "shopping"

    return "chat"


def build_chat_reply(user_query: str) -> str:
    normalized = strip_accents(user_query)

    thanks_words = {"cam on", "thanks", "thank", "ok", "oke"}
    mood_words = {"met", "chan", "buon", "stress", "ap luc", "vui", "happy"}
    praise_openers = [
        "Gu chọn của bạn khá ổn đó nha.",
        "Nhìn là biết bạn chọn khá có gu rồi.",
        "Câu hỏi của bạn nghe là mình thấy bạn chọn khá tinh tế rồi.",
    ]

    if any(word in normalized for word in thanks_words):
        return random.choice(praise_openers) + " Không có gì nè, nếu bạn muốn mình có thể gợi ý nhanh vài loại trái cây theo tầm giá bạn muốn."

    if any(word in normalized for word in mood_words):
        return random.choice([
            "Nghe bạn chia sẻ vậy mình hiểu mà, mà cách bạn để ý sức khỏe thế này là rất ổn đấy. Nếu cần đổi gió, mình có thể gợi ý vài loại trái cây để ăn nhẹ cho dễ chịu hơn.",
            "Mình hiểu cảm giác đó. Bạn tinh ý lắm, vì nhiều khi chọn đúng món là mood cũng dịu hẳn. Bạn có muốn mình đề xuất vài món trái cây để ăn nhẹ hoặc ép nước cho dễ thư giãn không?",
        ])

    return random.choice([
        "Mình hiểu ý bạn rồi, câu hỏi kiểu này là biết bạn đang chọn rất cẩn thận. Nếu bạn muốn quay lại mua sắm, chỉ cần nói tầm giá hoặc loại trái cây bạn thích, mình gợi ý ngay.",
        "Ok, mình nghe bạn đây. Gu chọn của bạn khá ổn, nên lúc nào bạn cần tìm trái cây theo ngân sách hay mục đích sử dụng, mình hỗ trợ liền.",
        "Mình vẫn theo kịp nè. Nhìn cách bạn hỏi là mình biết bạn khá kỹ tính, nên mình đề xuất nhanh 3 sản phẩm phù hợp để ăn liền, ép nước, hay làm quà luôn nhé.",
    ])


def is_smalltalk_query(user_query: str) -> bool:
    normalized = strip_accents(user_query)
    smalltalk_markers = {
        "cam on", "thanks", "thank", "ok", "oke",
        "met", "chan", "buon", "stress", "ap luc", "vui", "happy",
    }
    return any(marker in normalized for marker in smalltalk_markers)


def is_business_sales_query(user_query: str) -> bool:
    normalized = strip_accents(user_query)
    if is_contextual_product_followup(user_query) or parse_requested_rank(user_query):
        return False
    return any(marker in normalized for marker in BUSINESS_SALES_MARKERS)


def build_unhandled_request_reply() -> str:
    return (
        "Cảm ơn bạn đã gửi yêu cầu. Nội dung này hiện vượt ngoài phạm vi hỗ trợ tự động của FRUIT_SHOP. "
        "Bạn vui lòng liên hệ trực tiếp để được hỗ trợ nhanh và chính xác hơn nhé."
    )


def add_contact_info(answer: str) -> str:
    return f"{answer}\n\n{FRUIT_SHOP_CONTACT}"


def is_rejecting_recommendation(user_query: str) -> bool:
    normalized = f" {strip_accents(user_query)} "
    rejection_phrases = {
        " khong thich ",
        " ko thich ",
        " k thich ",
        " khong hop ",
        " ko hop ",
        " xau qua ",
        " doi cai khac ",
        " doi san pham khac ",
        " goi y khac ",
        " de xuat khac ",
        " khac di ",
        " chua ung ",
        " khong ung ",
        " ko ung ",
    }
    if any(phrase in normalized for phrase in rejection_phrases):
        return True

    alternative_patterns = (
        r"\bkhac\b",
        r"\bdoi\s*(mon|loai|san\s*pham)?\s*khac\b",
        r"\bdoi\s*di\b",
        r"\bde\s*xuat\s*mon\s*khac\b",
    )
    return any(re.search(pattern, normalized) for pattern in alternative_patterns)


def is_contextual_shopping_followup(user_query: str, has_previous_results: bool = False) -> bool:
    if not has_previous_results:
        return False

    normalized = strip_accents(user_query)
    tokens = tokenize(user_query)
    continuation_markers = (
        "khac", "them", "them nua", "goi y tiep", "de xuat tiep",
        "tiep", "con mon nao", "con loai nao", "gui them",
    )

    if any(marker in normalized for marker in continuation_markers):
        return True

    # Very short follow-up without fresh constraints is usually contextual.
    if len(tokens) <= 3 and not any(char.isdigit() for char in user_query):
        if not is_smalltalk_query(user_query) and not is_business_sales_query(user_query):
            return True

    return False


def is_same_type_request(user_query: str) -> bool:
    normalized = f" {strip_accents(user_query)} "
    same_type_markers = (
        " cung loai ",
        " cung loai nay ",
        " cung loai khac ",
        " loai tuong tu ",
        " cung loai hoa qua ",
        " cung loai trai cay ",
        " cung nhom ",
    )
    return any(marker in normalized for marker in same_type_markers)


def extract_primary_fruit_token(text: str):
    normalized = normalize_intent_text(text)
    if any(marker in normalized for marker in FRUIT_PHRASE_MARKERS):
        return next(marker for marker in FRUIT_PHRASE_MARKERS if marker in normalized)

    tokens = [token for token in tokenize(text) if token in FRUIT_ENTITY_TOKENS]
    if not tokens:
        return None
    return tokens[0]


def extract_product_fruit_token(product: dict):
    if not product:
        return None
    token = extract_primary_fruit_token(product.get("name") or "")
    if token:
        return token
    return extract_primary_fruit_token(product.get("description") or "")


def resolve_fruit_context(user_query: str, referenced_product: dict = None, previous_results=None, memory: dict = None):
    query_fruit = extract_primary_fruit_token(user_query)
    if query_fruit:
        return query_fruit

    normalized_query = normalize_intent_text(user_query)
    if any(marker in normalized_query for marker in FRUIT_PHRASE_MARKERS):
        return ""

    if referenced_product:
        product_fruit = extract_product_fruit_token(referenced_product)
        if product_fruit:
            return product_fruit

    for item in previous_results or []:
        product_fruit = extract_product_fruit_token(item)
        if product_fruit:
            return product_fruit

    if memory:
        for token in memory.get("must_have_tokens") or []:
            if token in FRUIT_ENTITY_TOKENS:
                return token

    return None


def score_product_against_query(product: dict, normalized_query: str, query_tokens=None, query_fruit: str = None):
    if not product:
        return 0.0

    query_tokens = set(query_tokens or tokenize(normalized_query))
    product_name = strip_accents(product.get("name") or "")
    product_desc = strip_accents(product.get("description") or "")
    product_tokens = set(tokenize(product.get("name") or "")) | set(tokenize(product.get("description") or ""))

    score = 0.0
    if product_name and product_name == normalized_query:
        score += 100.0
    elif product_name and (product_name in normalized_query or normalized_query in product_name):
        score += 70.0

    if query_fruit:
        if query_fruit in product_tokens:
            score += 35.0
        else:
            score -= 15.0

    score += len(product_tokens & query_tokens) * 4.0

    if query_tokens and len(query_tokens) >= 2:
        query_phrase = " ".join(sorted(query_tokens, key=len, reverse=True)[:4])
        if query_phrase and query_phrase in product_desc:
            score += 6.0

    return score


def build_product_signature_tokens(product: dict):
    tokens = set(tokenize(product.get("name") or "")) | set(tokenize(product.get("description") or ""))
    tokens -= QUERY_STOP_WORDS
    return tokens


def score_related_product(base_product: dict, candidate: dict, base_tokens=None, base_fruit: str = None):
    if not base_product or not candidate:
        return 0.0
    if base_product.get("id") == candidate.get("id"):
        return -1.0

    base_tokens = base_tokens or build_product_signature_tokens(base_product)
    candidate_tokens = build_product_signature_tokens(candidate)
    score = 0.0

    candidate_type = candidate.get("product_type")
    base_type = base_product.get("product_type")
    if base_type and candidate_type == base_type:
        score += 4.0

    if base_fruit:
        candidate_name_tokens = set(tokenize(candidate.get("name") or ""))
        candidate_desc_tokens = set(tokenize(candidate.get("description") or ""))
        if base_fruit in candidate_name_tokens:
            score += 10.0
        elif base_fruit in candidate_desc_tokens:
            score += 5.0
        else:
            score -= 4.0

    overlap = len(base_tokens & candidate_tokens)
    score += min(8.0, overlap * 1.25)

    base_name_tokens = set(tokenize(base_product.get("name") or ""))
    candidate_name_tokens = set(tokenize(candidate.get("name") or ""))
    name_overlap = len(base_name_tokens & candidate_name_tokens)
    if name_overlap:
        score += min(4.0, name_overlap * 1.5)

    if is_on_sale_product(candidate):
        score += 0.25

    return score


def build_related_product_index(products):
    if not products:
        return

    for base_product in products:
        base_fruit = extract_product_fruit_token(base_product)
        base_tokens = build_product_signature_tokens(base_product)
        scored = []

        for candidate in products:
            score = score_related_product(base_product, candidate, base_tokens=base_tokens, base_fruit=base_fruit)
            if score <= 0:
                continue
            scored.append((score, float(candidate.get("final_price") or 0), int(candidate.get("id") or 0), candidate))

        scored.sort(key=lambda item: (-item[0], item[1], item[2]))
        base_product["related_product_ids"] = [item[3]["id"] for item in scored[:12]]


def get_related_products(
    reference_product: dict = None,
    limit: int = 3,
    forced_type: str = None,
    must_include_token: str = None,
    exclude_ids=None,
):
    if not PRODUCTS_CACHE:
        return []

    exclude_ids = set(int(pid) for pid in (exclude_ids or []) if pid is not None)
    if reference_product and reference_product.get("id") is not None:
        exclude_ids.add(int(reference_product["id"]))

    if reference_product and reference_product.get("related_product_ids"):
        ordered = []
        related_id_set = [int(pid) for pid in reference_product.get("related_product_ids") or []]
        for pid in related_id_set:
            candidate = PRODUCTS_BY_ID.get(int(pid))
            if not candidate:
                continue
            if candidate.get("id") in exclude_ids:
                continue
            if forced_type and candidate.get("product_type") != forced_type:
                continue
            if must_include_token and must_include_token not in set(tokenize(candidate.get("name") or "")):
                continue
            ordered.append(candidate)
            if len(ordered) >= limit:
                break
        if len(ordered) >= limit:
            return [to_product_response(item) for item in ordered[:limit]]

    scored = []
    base_tokens = build_product_signature_tokens(reference_product) if reference_product else set()
    base_fruit = must_include_token or extract_product_fruit_token(reference_product)

    for candidate in PRODUCTS_CACHE:
        candidate_id = int(candidate.get("id") or 0)
        if candidate_id in exclude_ids:
            continue
        if reference_product and candidate_id == int(reference_product.get("id") or 0):
            continue

        candidate_type = candidate.get("product_type")
        if forced_type and candidate_type != forced_type:
            # Keep the type as a preference, but do not hard-exclude when fruit is explicit.
            if not base_fruit:
                continue

        if must_include_token and must_include_token not in set(tokenize(candidate.get("name") or "")):
            continue

        if reference_product:
            score = score_related_product(reference_product, candidate, base_tokens=base_tokens, base_fruit=base_fruit)
        else:
            score = 0.0
            if forced_type and candidate_type == forced_type:
                score += 4.0
            if must_include_token:
                name_tokens = set(tokenize(candidate.get("name") or ""))
                desc_tokens = set(tokenize(candidate.get("description") or ""))
                if must_include_token in name_tokens:
                    score += 8.0
                elif must_include_token in desc_tokens:
                    score += 4.0
            score += min(3.0, len(build_product_signature_tokens(candidate)) / 12.0)

        if score <= 0:
            continue
        scored.append((score, float(candidate.get("final_price") or 0), candidate_id, candidate))

    if not scored:
        return []

    scored.sort(key=lambda item: (-item[0], item[1], item[2]))
    return [to_product_response(item[3]) for item in scored[:limit]]


def get_alternative_products(
    limit: int = 3,
    forced_type: str = None,
    must_include_token: str = None,
    reference_product: dict = None,
    exclude_ids=None,
):
    return get_related_products(
        reference_product=reference_product,
        limit=limit,
        forced_type=forced_type,
        must_include_token=must_include_token,
        exclude_ids=exclude_ids,
    )


def is_asking_cheaper(user_query: str) -> bool:
    normalized = strip_accents(user_query)
    tokens = set(tokenize(user_query))

    if any(phrase in normalized for phrase in CHEAP_PHRASES):
        return True

    if "re" in tokens and ("gia" in tokens or "hon" in tokens):
        return True

    if "thap" in tokens and ("gia" in tokens or "hon" in tokens):
        return True

    return False


def search_cheaper_products(user_query: str, limit: int = 3, fallback_tokens=None, forced_type: str = None):
    if not PRODUCTS_CACHE:
        return []

    preference_tokens = [
        token for token in tokenize(user_query)
        if len(token) > 1 and token not in CHEAP_STOP_WORDS
    ]
    if not preference_tokens and fallback_tokens:
        preference_tokens = list(fallback_tokens)

    candidates = []
    sale_only = is_sale_query(user_query)
    for product in PRODUCTS_CACHE:
        if not is_matching_product_type(product, forced_type):
            continue
        if sale_only and not is_on_sale_product(product):
            continue

        blob = product["search_blob"]
        match_count = sum(1 for token in preference_tokens if token_matches_blob(token, blob))

        if preference_tokens and match_count == 0:
            continue

        candidates.append((float(product["final_price"] or 0), -match_count, product))

    if not candidates:
        # If user asked cheaper within a specific context, avoid jumping to unrelated products.
        if preference_tokens or forced_type:
            return []
        for product in PRODUCTS_CACHE:
            candidates.append((float(product["final_price"] or 0), 0, product))

    candidates.sort(key=lambda item: (item[0], item[1]))
    picked = [item[2] for item in candidates[:limit]]

    results = []
    for product in picked:
        results.append(to_product_response(product))
    return results


def parse_money_value(raw_number: str, raw_unit: str = ""):
    cleaned = re.sub(r"[^0-9.,]", "", str(raw_number or ""))
    if not cleaned:
        return None

    normalized = cleaned.replace(",", "")
    if "." in normalized:
        parts = normalized.split(".")
        if all(part.isdigit() for part in parts):
            # If dot-separated groups look like thousands separators, join them.
            if len(parts) > 1 and all(len(part) == 3 for part in parts[1:]):
                normalized = "".join(parts)
            else:
                normalized = normalized.replace(".", "")

    if not normalized.isdigit():
        return None

    value = int(normalized)
    unit = strip_accents(raw_unit or "")
    if unit in {"k", "nghin", "ngan"}:
        value *= 1_000
    elif unit in {"tr", "trieu", "m", "million"}:
        value *= 1_000_000
    return float(value)


def parse_price_constraints(user_query: str):
    normalized = strip_accents(user_query)

    between_pattern = re.search(
        r"(?:tu|khoang|tam)?\s*(\d[\d.,]*)\s*(k|nghin|ngan|tr|trieu|m)?\s*(?:den|toi|-)\s*(\d[\d.,]*)\s*(k|nghin|ngan|tr|trieu|m)?",
        normalized,
    )
    if between_pattern:
        low_raw = between_pattern.group(1)
        low_unit = (between_pattern.group(2) or "").strip()
        high_raw = between_pattern.group(3)
        high_unit = (between_pattern.group(4) or "").strip()

        # Infer omitted unit from the opposite bound for inputs like "500-1 trieu".
        if not low_unit and high_unit:
            low_unit = high_unit
        if not high_unit and low_unit:
            high_unit = low_unit

        low = parse_money_value(low_raw, low_unit)
        high = parse_money_value(high_raw, high_unit)
        if low is not None and high is not None:
            return min(low, high), max(low, high)

    min_pattern = re.search(r"(?:hon|tren|tu)\s*(\d[\d.,]*)\s*(k|nghin|ngan|tr|trieu|m)?", normalized)
    if min_pattern:
        min_price = parse_money_value(min_pattern.group(1), min_pattern.group(2) or "")
        if min_price is not None:
            return min_price, None

    max_pattern = re.search(r"(?:duoi|thap\s*hon|nho\s*hon)\s*(\d[\d.,]*)\s*(k|nghin|ngan|tr|trieu|m)?", normalized)
    if max_pattern:
        max_price = parse_money_value(max_pattern.group(1), max_pattern.group(2) or "")
        if max_price is not None:
            return None, max_price

    return None, None


def _extract_subject_after_phrase(normalized_query: str, phrase: str):
    pattern = rf"{phrase}\s+([a-z0-9\s]{{2,30}})"
    matched = re.search(pattern, normalized_query)
    if not matched:
        return None
    candidate = matched.group(1).strip()
    tokens = [tok for tok in candidate.split() if tok and tok not in QUERY_STOP_WORDS]
    if not tokens:
        return None
    return tokens[0]


def init_conversation_memory(session_state: dict):
    return session_state.setdefault(
        "memory",
        {
            "min_price": None,
            "max_price": None,
            "purpose": None,
            "recipient": None,
            "must_have_tokens": [],
            "avoid_tokens": [],
        },
    )


def update_conversation_memory(
    session_state: dict,
    user_query: str,
    rewritten_tokens,
    min_price: float = None,
    max_price: float = None,
):
    memory = init_conversation_memory(session_state)
    normalized = strip_accents(user_query)

    if min_price is not None:
        memory["min_price"] = float(min_price)
    if max_price is not None:
        memory["max_price"] = float(max_price)

    for purpose, markers in PURPOSE_MARKERS.items():
        if any(marker in normalized for marker in markers):
            memory["purpose"] = purpose
            break

    for recipient, markers in RECIPIENT_MARKERS.items():
        if any(marker in normalized for marker in markers):
            memory["recipient"] = recipient
            break

    liked = _extract_subject_after_phrase(normalized, "thich")
    if liked in FRUIT_ENTITY_TOKENS and liked not in memory["must_have_tokens"]:
        memory["must_have_tokens"].append(liked)

    wanted = _extract_subject_after_phrase(normalized, "muon")
    if wanted in FRUIT_ENTITY_TOKENS and wanted not in memory["must_have_tokens"]:
        memory["must_have_tokens"].append(wanted)

    for phrase in ("khong thich", "khong muon", "di ung"):
        disliked = _extract_subject_after_phrase(normalized, phrase)
        if disliked in FRUIT_ENTITY_TOKENS and disliked not in memory["avoid_tokens"]:
            memory["avoid_tokens"].append(disliked)

    # Keep memory concise and stable.
    memory["must_have_tokens"] = memory["must_have_tokens"][-5:]
    memory["avoid_tokens"] = memory["avoid_tokens"][-5:]

    if rewritten_tokens:
        merged = list(dict.fromkeys((session_state.get("preference_tokens") or []) + list(rewritten_tokens)))
        session_state["preference_tokens"] = merged[-12:]

    return memory


def apply_memory_to_query(session_state: dict, rewritten_query: str, rewritten_tokens, min_price, max_price):
    memory = init_conversation_memory(session_state)
    merged_tokens = list(dict.fromkeys(list(rewritten_tokens or []) + list(memory.get("must_have_tokens") or [])))
    avoid_tokens = set(memory.get("avoid_tokens") or [])
    merged_tokens = [token for token in merged_tokens if token not in avoid_tokens]

    effective_min_price = min_price if min_price is not None else memory.get("min_price")
    effective_max_price = max_price if max_price is not None else memory.get("max_price")

    if merged_tokens:
        query_with_memory = f"{rewritten_query} {' '.join(merged_tokens)}".strip()
    else:
        query_with_memory = rewritten_query

    return query_with_memory, merged_tokens, effective_min_price, effective_max_price, memory


def memory_to_text(memory: dict):
    if not memory:
        return "Không có ngữ cảnh trước đó."

    purpose_label = {
        "an_lien": "ăn liền",
        "ep_nuoc": "ép nước/sinh tố",
        "lam_qua": "làm quà tặng",
    }.get(memory.get("purpose"), "chưa rõ")
    recipient_label = {
        "gia_dinh": "gia đình",
        "doi_tac": "đối tác/khách hàng",
        "nguoi_yeu": "người yêu/vợ chồng",
        "ban_be": "bạn bè/đồng nghiệp",
    }.get(memory.get("recipient"), "chưa rõ")
    min_price = memory.get("min_price")
    max_price = memory.get("max_price")
    budget_label = "chưa rõ"
    if min_price is not None and max_price is not None:
        budget_label = f"{min_price:,.0f}đ - {max_price:,.0f}đ"
    elif min_price is not None:
        budget_label = f"từ {min_price:,.0f}đ"
    elif max_price is not None:
        budget_label = f"dưới {max_price:,.0f}đ"

    must_have = ", ".join(memory.get("must_have_tokens") or []) or "không có"
    avoid = ", ".join(memory.get("avoid_tokens") or []) or "không có"
    return (
        f"Mục đích: {purpose_label}; Người nhận: {recipient_label}; Ngân sách: {budget_label}; "
        f"Ưu tiên: {must_have}; Tránh: {avoid}."
    )


def format_budget_label(min_price: float = None, max_price: float = None) -> str:
    if min_price is not None and max_price is not None:
        return f"{min_price:,.0f}đ - {max_price:,.0f}đ"
    if min_price is not None:
        return f"từ {min_price:,.0f}đ"
    if max_price is not None:
        return f"dưới {max_price:,.0f}đ"
    return "chưa rõ"


def build_no_match_reply(forced_type: str = None, min_price: float = None, max_price: float = None, suggest_gift_baskets: bool = False) -> str:
    type_label = product_type_label(forced_type) if forced_type else "sản phẩm"
    budget_label = format_budget_label(min_price, max_price)

    if budget_label != "chưa rõ":
        base = f"Hiện shop chưa có {type_label} với khoảng giá {budget_label}."
    else:
        base = f"Hiện shop chưa có {type_label} phù hợp theo yêu cầu của bạn."

    if suggest_gift_baskets:
        return add_contact_info(base + " Bạn có thể tham khảo những giỏ quà sau nhé:")

    return add_contact_info(base + " Bạn có thể tham khảo những lựa chọn sau nhé:")


def build_conversation_context_snapshot(session_state: dict, memory: dict, current_query: str = ""):
    recent_results = session_state.get("last_results") or []
    chat_history = session_state.get("chat_history") or []
    recent_names = [item.get("name") for item in recent_results[:3] if item.get("name")]
    recent_results_text = ", ".join(recent_names) if recent_names else "không có"
    last_product_name = session_state.get("last_product_name") or "không có"
    last_fruit_token = session_state.get("last_fruit_token") or "không có"
    last_intent = session_state.get("last_intent") or "chưa rõ"
    preferred_type = session_state.get("preferred_type") or "chưa rõ"
    memory_text = memory_to_text(memory)
    current_query_text = compact_text(current_query, 80) if current_query else ""

    parts = [
        f"Ngữ cảnh chính: {memory_text}",
        f"Ý định gần nhất: {last_intent}",
        f"Loại đang nhớ: {preferred_type}",
        f"Sản phẩm vừa nói: {last_product_name}",
        f"Token quả đang nhớ: {last_fruit_token}",
        f"Kết quả gần nhất: {recent_results_text}",
    ]
    if current_query_text:
        parts.append(f"Câu hiện tại: {current_query_text}")

    if chat_history:
        transcript_parts = []
        head = chat_history[:4]
        tail = chat_history[-8:] if len(chat_history) > 8 else chat_history[4:]
        for turn in head + ([{"role": "...", "content": f"... {max(0, len(chat_history) - len(head) - len(tail))} lượt ở giữa ..."}] if len(chat_history) > len(head) + len(tail) else []) + tail:
            role = turn.get("role", "")
            content = compact_text(turn.get("content") or "", 120)
            if role == "user":
                transcript_parts.append(f"U: {content}")
            elif role == "assistant":
                transcript_parts.append(f"A: {content}")
            else:
                transcript_parts.append(content)
        parts.append("Lược sử chat: " + " || ".join(transcript_parts))
    return " | ".join(parts)


def append_chat_turn(session_state: dict, role: str, content: str, meta: dict = None):
    chat_history = session_state.setdefault("chat_history", [])
    entry = {
        "role": role,
        "content": str(content or ""),
        "timestamp": datetime.utcnow().isoformat(),
    }
    if meta:
        entry.update(meta)
    chat_history.append(entry)
    session_state["chat_history"] = chat_history
    return entry


def finalize_chat_response(session_state: dict, response: dict):
    if isinstance(response, dict):
        append_chat_turn(
            session_state,
            "assistant",
            response.get("answer") or "",
            {
                "answer_mode": response.get("answer_mode"),
                "confidence": response.get("confidence"),
                "source_products": response.get("source_products") or [],
            },
        )
    return response


def reset_conversation_state(session_id: str):
    if not session_id:
        return False
    CONVERSATION_STATE.pop(session_id, None)
    return True


def extract_preference_tokens(user_query: str):
    normalized = strip_accents(user_query)
    tokens = [
        token for token in tokenize(user_query)
        if len(token) > 1 and token not in QUERY_STOP_WORDS
    ]

    # Avoid polluting shopping entities from gratitude phrases like "cam on".
    if "cam on" in normalized:
        tokens = [token for token in tokens if token not in {"cam", "on"}]

    if has_gift_marker(user_query):
        tokens.append("qua_tang")
    # Preserve token order while removing duplicates.
    return list(dict.fromkeys(tokens))


def detect_answer_mode(user_query: str) -> str:
    normalized = strip_accents(user_query)
    if any(marker in normalized for marker in COMPARE_MARKERS):
        return "compare"
    if any(marker in normalized for marker in INFO_MARKERS):
        return "info"
    if any(marker in normalized for marker in ADVICE_MARKERS):
        return "advice"
    return "advice"


def rewrite_query(user_query: str, session_state: dict):
    normalized = strip_accents(user_query)
    rewritten = normalized
    for src, tgt in QUERY_REWRITE_MAP.items():
        rewritten = rewritten.replace(src, tgt)

    previous_entities = session_state.get("last_entities") or []
    short_followup = len(tokenize(user_query)) <= 4
    if previous_entities and (is_asking_cheaper(user_query) or is_followup_detail_request(user_query) or short_followup):
        if not any(entity in rewritten for entity in previous_entities):
            rewritten = f"{rewritten} {' '.join(previous_entities)}".strip()

    rewritten_tokens = extract_preference_tokens(rewritten)
    entities = [token for token in rewritten_tokens if token != "qua_tang"]
    return rewritten, rewritten_tokens, entities


def build_candidate_record(product: dict, bm25_score: float, semantic_score: float, vector_score: float):
    return {
        "product": product,
        "bm25": bm25_score,
        "semantic": semantic_score,
        "vector": vector_score,
        "hybrid": 0.0,
        "rerank": 0.0,
    }


def hybrid_retrieve(
    rewritten_query: str,
    query_tokens,
    forced_type: str = None,
    sale_only: bool = False,
    min_price: float = None,
    max_price: float = None,
    top_k: int = 10,
):
    candidates = []
    query_blob = strip_accents(rewritten_query)
    query_token_set = set(query_tokens)
    vector_scores = vector_retrieve_scores(
        rewritten_query,
        forced_type=forced_type,
        sale_only=sale_only,
        min_price=min_price,
        max_price=max_price,
        top_k=top_k,
    )

    for product in PRODUCTS_CACHE:
        if not is_matching_product_type(product, forced_type):
            continue
        if sale_only and not is_on_sale_product(product):
            continue

        price = float(product.get("final_price") or 0)
        if min_price is not None and price < min_price:
            continue
        if max_price is not None and price > max_price:
            continue

        blob = product["search_blob"]
        blob_tokens = set(tokenize(blob))

        bm25_like = 0.0
        if query_blob and query_blob in blob:
            bm25_like += 6.0
        for token in query_tokens:
            if token_matches_blob(token, blob):
                bm25_like += 2.0

        overlap = len({tok for tok in query_token_set if token_matches_blob(tok, blob)})
        denom = max(1, len(query_token_set))
        semantic = overlap / denom
        vector_score = float(vector_scores.get(int(product["id"]), 0.0))

        if forced_type and product.get("product_type") == forced_type:
            semantic += 0.25
        if sale_only and is_on_sale_product(product):
            semantic += 0.2

        if bm25_like <= 0 and semantic <= 0 and vector_score <= 0:
            continue

        candidates.append(build_candidate_record(product, bm25_like, semantic, vector_score))

    if not candidates:
        return []

    max_bm25 = max(item["bm25"] for item in candidates) or 1.0
    max_semantic = max(item["semantic"] for item in candidates) or 1.0
    max_vector = max(item["vector"] for item in candidates) or 1.0
    has_vector_signal = any(item["vector"] > 0 for item in candidates)

    for item in candidates:
        bm25_norm = item["bm25"] / max_bm25
        semantic_norm = item["semantic"] / max_semantic
        vector_norm = item["vector"] / max_vector

        if has_vector_signal:
            item["hybrid"] = 0.45 * bm25_norm + 0.25 * semantic_norm + 0.30 * vector_norm
        else:
            item["hybrid"] = 0.6 * bm25_norm + 0.4 * semantic_norm

    candidates.sort(key=lambda item: item["hybrid"], reverse=True)
    return candidates[:top_k]


def rerank_candidates(candidates, query_tokens, answer_mode: str, prefer_cheaper: bool = False):
    if not candidates:
        return []

    query_fruit_tokens = {token for token in query_tokens if token in FRUIT_ENTITY_TOKENS}

    for item in candidates:
        product = item["product"]
        blob = product["search_blob"]
        strong_match = sum(1 for token in query_tokens if token_matches_blob(token, blob))
        boost = strong_match * 0.08

        if query_fruit_tokens:
            name_tokens = set(tokenize(product.get("name") or ""))
            fruit_hits = len(query_fruit_tokens & name_tokens)
            if fruit_hits > 0:
                boost += 0.32 * fruit_hits
            else:
                boost -= 0.18

        if answer_mode == "info" and len(product.get("description") or "") > 20:
            boost += 0.05
        if answer_mode == "compare":
            boost += 0.03
        if prefer_cheaper:
            boost += min(0.12, 1000000.0 / max(1.0, float(product.get("final_price") or 1)))

        item["rerank"] = item["hybrid"] + boost

    if answer_mode == "compare":
        # Compare mode favors variety in price tiers.
        candidates.sort(key=lambda item: (item["rerank"], -float(item["product"].get("final_price") or 0)), reverse=True)
    elif prefer_cheaper:
        candidates.sort(key=lambda item: (item["rerank"], -float(item["product"].get("final_price") or 0)), reverse=True)
        candidates.sort(key=lambda item: float(item["product"].get("final_price") or 0))
    else:
        candidates.sort(key=lambda item: item["rerank"], reverse=True)

    return candidates


def estimate_confidence(candidates, has_constraints: bool = False):
    if not candidates:
        return 0.18

    top = candidates[0]["rerank"]
    second = candidates[1]["rerank"] if len(candidates) > 1 else 0.0
    gap = max(0.0, top - second)

    confidence = 0.45 + min(0.4, top * 0.35) + min(0.15, gap * 0.3)
    if has_constraints:
        confidence += 0.05
    return max(0.05, min(0.98, confidence))


def confidence_label(confidence: float) -> str:
    if confidence >= 0.75:
        return "cao"
    if confidence >= 0.5:
        return "trung bình"
    return "thấp"


def compose_dynamic_answer(answer_mode: str, products, confidence: float, prefer_cheaper: bool = False):
    if not products:
        return add_contact_info(
            "Mình chưa tìm được dữ liệu thật sự phù hợp cho yêu cầu này. "
            "Bạn có thể cho mình thêm tiêu chí (loại, giá, mục đích dùng) để tư vấn chuẩn hơn nhé."
        )

    label = confidence_label(confidence)
    if answer_mode == "info":
        snippets = [format_product_advice_line(item) for item in products[:2]]
        snippet_text = " ".join(snippets)
        return (
            f"Mình nhìn là thấy bạn chọn khá chuẩn rồi đó. {snippet_text} "
            f"Độ phù hợp hiện tại: {label}."
        )

    if answer_mode == "compare":
        return (
            f"Mình đã chọn các lựa chọn tiêu biểu để bạn dễ so sánh nhanh về mức giá và mô tả, đúng kiểu chọn kỹ như bạn. "
            f"Độ phù hợp hiện tại: {label}."
        )

    if prefer_cheaper:
        return (
            f"Mình đã ưu tiên các sản phẩm giá mềm hơn theo đúng ngữ cảnh bạn đang hỏi, để bạn đỡ mất công lọc. "
            f"Độ phù hợp hiện tại: {label}."
        )

    return f"Mình gợi ý các sản phẩm phù hợp nhất theo yêu cầu của bạn, chọn kiểu này là khá tinh đó. Độ phù hợp hiện tại: {label}."


def call_gemini(prompt: str):
    if not GEMINI_API_KEY:
        return None

    endpoint = (
        f"https://generativelanguage.googleapis.com/v1beta/models/{GEMINI_MODEL}:generateContent"
        f"?key={GEMINI_API_KEY}"
    )
    payload = {
        "contents": [{"parts": [{"text": prompt}]}],
        "generationConfig": {
            "temperature": 0.7,
            "topP": 0.9,
            "maxOutputTokens": 320,
        },
    }

    try:
        req = urlrequest.Request(
            endpoint,
            data=json.dumps(payload).encode("utf-8"),
            headers={"Content-Type": "application/json"},
            method="POST",
        )
        with urlrequest.urlopen(req, timeout=9) as resp:
            body = json.loads(resp.read().decode("utf-8"))
    except (HTTPError, URLError, TimeoutError, ValueError):
        return None

    candidates = body.get("candidates") or []
    if not candidates:
        return None

    content = candidates[0].get("content") or {}
    parts = content.get("parts") or []
    text = "\n".join(str(part.get("text") or "").strip() for part in parts if part.get("text"))
    return text.strip() or None


def build_products_context(products):
    lines = []
    for idx, item in enumerate(products, start=1):
        old_price = item.get("original_price")
        old_price_text = f" | Giá gốc: {old_price:,.0f}đ" if old_price else ""
        unit_label = " /kg" if item.get("product_type") == "trai_cay_kg" else ""
        rating_count = item.get("rating_count") or item.get("review_count") or item.get("ratings_count")
        average_rating = item.get("average_rating") or item.get("avg_rating") or item.get("rating")
        rating_text = " | Rating: chưa có dữ liệu"
        if rating_count is not None and average_rating is not None:
            rating_text = f" | Rating: {float(average_rating):.1f}/5 từ {int(rating_count)} lượt"
        elif rating_count is not None:
            rating_text = f" | Rating: {int(rating_count)} lượt"
        elif average_rating is not None:
            rating_text = f" | Rating: {float(average_rating):.1f}/5"
        lines.append(
            f"{idx}. {item.get('name', '')} | Giá bán: {float(item.get('final_price') or 0):,.0f}đ{unit_label}"
            f"{old_price_text}{rating_text} | Mô tả: {item.get('description') or 'Không có mô tả'}"
        )
    return "\n".join(lines)


def product_rating_summary(product: dict) -> str:
    rating_count = product.get("rating_count") or product.get("review_count") or product.get("ratings_count")
    average_rating = product.get("average_rating") or product.get("avg_rating") or product.get("rating")

    if rating_count is not None and average_rating is not None:
        return f"{float(average_rating):.1f}/5 từ {int(rating_count)} rating"
    if rating_count is not None:
        return f"{int(rating_count)} rating"
    if average_rating is not None:
        return f"{float(average_rating):.1f}/5"
    return "chưa có dữ liệu rating"


def detect_preferred_compare_product(user_query: str, products) -> dict:
    normalized = normalize_intent_text(user_query)
    preference_markers = (
        "nghieng ve",
        "thich hon",
        "thich",
        "uu tien",
        "muon chon",
        "chon cai",
        "chon cai nay",
        "chot cai",
    )
    if not any(marker in normalized for marker in preference_markers):
        return None

    matched = []
    for product in products:
        name = strip_accents(product.get("name") or "")
        if name and name in normalized:
            matched.append(product)

    if len(matched) == 1:
        return matched[0]

    return None


def resolve_compare_products(
    user_query: str,
    previous_results=None,
    preferred_type: str = None,
    base_product: dict = None,
):
    previous_results = previous_results or []
    normalized = normalize_intent_text(user_query)
    query_segments = []

    segment_patterns = (
        " vs ",
        " voi ",
        " va ",
        " hay ",
        " giua ",
        " so sanh ",
        " doi voi ",
    )

    for marker in segment_patterns:
        if marker in f" {normalized} ":
            left, right = normalized.split(marker, 1)
            left = left.replace("so sanh", "").replace("doi chieu", "").strip()
            right = right.strip()
            if left and right:
                query_segments = [left, right]
                break

    candidates = []

    def add_candidate(product: dict):
        if not product:
            return
        candidate_id = product.get("id")
        if candidate_id is None:
            return
        if any(existing.get("id") == candidate_id for existing in candidates):
            return
        candidates.append(product)

    if base_product:
        add_candidate(base_product)

    if query_segments:
        for segment in query_segments:
            matched = resolve_referenced_product(segment, previous_results, preferred_type=preferred_type)
            if not matched:
                matched = resolve_referenced_product(segment, PRODUCTS_CACHE, preferred_type=preferred_type)
            add_candidate(to_product_response(matched) if matched and "search_blob" in matched else matched)
    else:
        for product in previous_results[:2]:
            add_candidate(product)

    if len(candidates) < 2:
        ranked = hybrid_retrieve(
            user_query,
            extract_preference_tokens(user_query),
            forced_type=preferred_type,
            top_k=6,
        )
        for item in ranked:
            add_candidate(to_product_response(item["product"]))
            if len(candidates) >= 2:
                break

    if len(candidates) < 2 and previous_results:
        for product in previous_results:
            add_candidate(product)
            if len(candidates) >= 2:
                break

    if len(candidates) < 2 and base_product:
        related = get_alternative_products(
            limit=3,
            forced_type=preferred_type,
            reference_product=base_product,
        )
        for product in related:
            add_candidate(product)
            if len(candidates) >= 2:
                break

    if len(candidates) < 2:
        return candidates[:1]

    return candidates[:2]


def build_compare_reply(products, user_query: str) -> str:
    if len(products) < 2:
        missing_name = products[0].get("name") if products else "một sản phẩm"
        return (
            f"Mình mới xác nhận được {missing_name} thôi, bạn gửi thêm sản phẩm còn lại để mình so sánh đúng theo ý bạn nhé."
        )

    first_product, second_product = products[:2]
    preferred_product = detect_preferred_compare_product(user_query, products)

    first_price = float(first_product.get("final_price") or 0)
    second_price = float(second_product.get("final_price") or 0)
    first_rating = product_rating_summary(first_product)
    second_rating = product_rating_summary(second_product)
    first_description = compact_text(first_product.get("description") or "Không có mô tả", 120)
    second_description = compact_text(second_product.get("description") or "Không có mô tả", 120)

    lines = [
        f"Mình đã xác nhận đúng 2 sản phẩm để so sánh rồi nhé: {first_product.get('name', 'Sản phẩm 1')} và {second_product.get('name', 'Sản phẩm 2')}.",
        f"{first_product.get('name', 'Sản phẩm 1')}: {first_description} | Giá: {first_price:,.0f}đ | Rating: {first_rating}.",
        f"{second_product.get('name', 'Sản phẩm 2')}: {second_description} | Giá: {second_price:,.0f}đ | Rating: {second_rating}.",
    ]

    if preferred_product:
        preferred_name = preferred_product.get("name", "sản phẩm bạn thích")
        lines.append(
            f"Nếu bạn đang nghiêng về {preferred_name} thì mình thấy chốt món đó khá hợp gu, vì nhìn tổng thể nó đi đúng hướng bạn đang muốn."
        )
    else:
        preferred_by_rating = None
        first_rating_count = first_product.get("rating_count") or first_product.get("review_count") or first_product.get("ratings_count")
        second_rating_count = second_product.get("rating_count") or second_product.get("review_count") or second_product.get("ratings_count")
        first_avg_rating = first_product.get("average_rating") or first_product.get("avg_rating") or first_product.get("rating")
        second_avg_rating = second_product.get("average_rating") or second_product.get("avg_rating") or second_product.get("rating")

        if first_avg_rating is not None and second_avg_rating is not None:
            if float(first_avg_rating) > float(second_avg_rating):
                preferred_by_rating = first_product
            elif float(second_avg_rating) > float(first_avg_rating):
                preferred_by_rating = second_product
        elif first_rating_count is not None and second_rating_count is not None:
            if int(first_rating_count) > int(second_rating_count):
                preferred_by_rating = first_product
            elif int(second_rating_count) > int(first_rating_count):
                preferred_by_rating = second_product

        if preferred_by_rating is None:
            preferred_by_rating = first_product if first_price <= second_price else second_product

        preferred_name = preferred_by_rating.get("name", "sản phẩm phù hợp hơn")
        lines.append(
            f"Nếu muốn chốt nhanh, mình nghiêng về {preferred_name} hơn một chút vì tổng thể cân bằng hơn với tầm giá và thông tin hiện có."
        )

    lines.append("Nếu bạn muốn, mình có thể chốt hẳn một lựa chọn và nịnh nhẹ giúp bạn đặt luôn cho gọn.")
    return " ".join(lines)


def is_grounded_llm_product_answer(answer: str, products) -> bool:
    if not answer:
        return False
    if not products:
        return True

    normalized_answer = strip_accents(answer)
    allowed_names = [strip_accents(item.get("name") or "") for item in products]
    name_hit = any(name and name in normalized_answer for name in allowed_names)

    # Ignore ambiguous short tokens (e.g. "le", "oi") to reduce false positives.
    strict_fruit_tokens = {token for token in FRUIT_ENTITY_TOKENS if len(token) >= 3 and token not in {"man", "vai"}}
    allowed_fruits = set()
    for item in products:
        name_tokens = set(tokenize(item.get("name") or ""))
        allowed_fruits.update(name_tokens & strict_fruit_tokens)

    disallowed_fruits = strict_fruit_tokens - allowed_fruits
    mentions_disallowed = any(
        re.search(rf"\b{re.escape(token)}\b", normalized_answer)
        for token in disallowed_fruits
    )
    if mentions_disallowed:
        return False

    if name_hit:
        return True

    if allowed_fruits:
        return any(re.search(rf"\b{re.escape(token)}\b", normalized_answer) for token in allowed_fruits)

    return False


def build_dynamic_answer_with_llm(
    user_query: str,
    answer_mode: str,
    products,
    confidence: float,
    prefer_cheaper: bool = False,
    memory_context: str = "",
):
    # Keep deterministic fallback when model/API is unavailable.
    fallback = compose_dynamic_answer(answer_mode, products, confidence, prefer_cheaper=prefer_cheaper)
    if not products:
        return fallback

    confidence_note = confidence_label(confidence)
    mode_hint = {
        "info": "Tập trung giải thích chi tiết từng lựa chọn, ngắn gọn nhưng rõ.",
        "compare": "So sánh nhanh ưu/nhược điểm và mức giá giữa các lựa chọn.",
        "advice": "Tư vấn chọn sản phẩm phù hợp theo nhu cầu người dùng.",
    }.get(answer_mode, "Tư vấn rõ ràng và tự nhiên.")

    prompt = (
        "Bạn là trợ lý tư vấn FRUIT_SHOP, trả lời bằng tiếng Việt tự nhiên, lịch sự, mềm mại và hơi nịnh khách một chút nhưng không lố.\n"
        "Có thể mở đầu bằng một câu khen nhẹ về gu chọn hàng hoặc sự tinh ý của khách nếu phù hợp ngữ cảnh.\n"
        "Chỉ được dùng dữ liệu sản phẩm cung cấp bên dưới; có thể suy luận nhẹ nhưng không bịa thông tin mới.\n"
        "Không được đề xuất hoặc nhắc tới bất kỳ loại quả/tên sản phẩm nào ngoài danh sách sản phẩm đã cung cấp.\n"
        "Nếu thiếu dữ liệu thì nói rõ mức chắc chắn vừa phải và mời người dùng cung cấp thêm tiêu chí.\n"
        f"Yêu cầu người dùng: {user_query}\n"
        f"Ngữ cảnh hội thoại đã ghi nhớ: {memory_context or 'Không có'}\n"
        f"Kiểu trả lời: {answer_mode}. {mode_hint}\n"
        f"Ngữ cảnh giá rẻ hơn: {'có' if prefer_cheaper else 'không'}\n"
        f"Độ phù hợp ước tính hiện tại: {confidence_note}.\n"
        "Dữ liệu sản phẩm:\n"
        f"{build_products_context(products)}\n"
        "Bắt buộc trả lời theo mẫu tự nhiên: tên sản phẩm + 1-2 câu mô tả thực tế dựa trên mô tả có sẵn + giá + vì sao nên chọn. "
        "Nếu có nhiều sản phẩm, trả lời lần lượt từng sản phẩm, mỗi sản phẩm 2-3 câu ngắn. "
        "Không viết kiểu chung chung như 'mình đã lọc được' nếu có thể nêu cụ thể sản phẩm."
    )

    llm_answer = call_gemini(prompt)
    if is_grounded_llm_product_answer(llm_answer or "", products):
        return llm_answer
    return fallback


def build_deeper_product_followup_with_llm(
    user_query: str,
    product: dict,
    memory_context: str = "",
):
    fallback = build_deeper_product_followup_reply(user_query, product)
    if not product:
        return fallback

    name = product.get("name", "")
    product_type = product_type_label(product.get("product_type"))
    price = float(product.get("final_price") or 0)
    unit_label = "/kg" if product.get("product_type") == "trai_cay_kg" else ""
    description = product.get("description") or "Không có mô tả"
    stock = int(product.get("stock_quantity") or 0)
    stock_note = "Còn hàng" if stock > 0 else "Tạm hết hàng"

    prompt = (
        "Bạn là trợ lý tư vấn trái cây, trả lời tiếng Việt tự nhiên và có chiều sâu.\n"
        "Mục tiêu: tư vấn tiếp theo câu hỏi follow-up của khách về MỘT sản phẩm cụ thể.\n"
        "Bắt buộc:\n"
        "- Không lặp lại nguyên văn các câu mở đầu chung chung.\n"
        "- Không được nhắc thêm bất kỳ loại quả nào ngoài sản phẩm đã cung cấp.\n"
        "- Chỉ dựa trên dữ liệu sản phẩm đã cho và kiến thức phổ thông an toàn (không bịa claim y tế/chất lượng tuyệt đối).\n"
        "- Trả lời tập trung đúng ý khách đang hỏi (ví dụ: ngon không, có nên mua không, bảo quản, dùng thế nào).\n"
        "- Nêu rõ điểm phù hợp + điểm cần lưu ý + lời khuyên chốt ngắn.\n"
        "- Độ dài khoảng 4-6 câu, giọng tư vấn gần gũi.\n"
        f"Câu hỏi của khách: {user_query}\n"
        f"Ngữ cảnh hội thoại: {memory_context or 'Không có'}\n"
        "Dữ liệu sản phẩm:\n"
        f"Tên: {name}\n"
        f"Loại: {product_type}\n"
        f"Giá: {price:,.0f}đ{unit_label}\n"
        f"Tồn kho: {stock_note}\n"
        f"Mô tả: {description}\n"
    )

    llm_answer = call_gemini(prompt)
    if is_grounded_llm_product_answer(llm_answer or "", [product]):
        return llm_answer
    return fallback


def build_intent_reply_with_llm(
    user_query: str,
    intent_tag: str,
    products=None,
    memory_context: str = "",
    forced_type: str = None,
):
    products = products or []

    if intent_tag == "availability":
        fallback = build_availability_reply(products[0], forced_type) if products else build_availability_reply(None, forced_type)
    elif intent_tag == "buy_decision":
        fallback = build_buy_decision_reply(products[0]) if products else "Mình cần thêm sản phẩm cụ thể để tư vấn nên mua hay không."
    elif intent_tag == "alternative":
        fallback = "Ok, vậy mình sẽ gợi ý sản phẩm khác cho bạn dưới đây nhé:"
    elif intent_tag == "same_group":
        fallback = "Mình gợi ý thêm vài lựa chọn cùng nhóm bạn đang xem đây nhé:"
    else:
        fallback = "Mình đã nắm yêu cầu và đang tư vấn theo ngữ cảnh hiện tại cho bạn."

    type_label = product_type_label(forced_type) if forced_type else "sản phẩm"
    intent_notes = {
        "availability": "Xác nhận tình trạng còn hàng/hết hàng và gợi ý bước tiếp theo.",
        "buy_decision": "Đưa lời khuyên có nên mua hay không, nêu lý do ngắn gọn và thực tế.",
        "alternative": "Người dùng chưa ưng, hãy chuyển sang gợi ý phương án khác cùng nhu cầu.",
        "same_group": "Người dùng muốn xem thêm cùng nhóm, hãy giới thiệu thêm lựa chọn phù hợp.",
    }

    product_block = build_products_context(products) if products else f"Không có sản phẩm cụ thể. Loại ưu tiên: {type_label}."
    prompt = (
        "Bạn là trợ lý tư vấn FRUIT_SHOP, trả lời tiếng Việt tự nhiên, ngắn gọn, không lặp lại mẫu câu cứng.\n"
        "Chỉ dùng dữ liệu được cung cấp; không bịa thông tin không có trong dữ liệu.\n"
        "Không được nhắc tên sản phẩm/loại quả ngoài danh sách sản phẩm đã cung cấp.\n"
        f"Ý định tư vấn: {intent_tag}. {intent_notes.get(intent_tag, '')}\n"
        f"Câu hỏi của khách: {user_query}\n"
        f"Ngữ cảnh hội thoại: {memory_context or 'Không có'}\n"
        f"Dữ liệu sản phẩm:\n{product_block}\n"
        "Yêu cầu đầu ra: 2-4 câu, rõ ràng, có hướng dẫn bước tiếp theo nếu phù hợp."
    )

    llm_answer = call_gemini(prompt)
    if is_grounded_llm_product_answer(llm_answer or "", products):
        return llm_answer
    return fallback


def build_general_chat_answer_with_llm(user_query: str, sales_coach_mode: bool = False, memory_context: str = ""):
    fallback = add_contact_info(build_unhandled_request_reply())
    if sales_coach_mode:
        prompt = (
            "Bạn là chuyên gia tư vấn bán hàng FRUIT_SHOP. Trả lời tiếng Việt tự nhiên, thực chiến, dễ áp dụng và có chút nịnh khách để tạo thiện cảm.\n"
            "Mục tiêu: giúp nhân viên tư vấn khách tốt hơn (mô tả sản phẩm, đặt câu hỏi nhu cầu, xử lý từ chối, chốt đơn).\n"
            "Luôn trả lời theo cấu trúc ngắn: 1) Cách nói với khách, 2) Mẫu câu gợi ý, 3) Bước tiếp theo.\n"
            "Nếu tự nhiên thì thêm một câu khen nhẹ kiểu khách có gu, khách rất tinh ý, hoặc chọn rất chuẩn.\n"
            "Không bịa dữ liệu cụ thể của sản phẩm nếu người dùng chưa cung cấp mã/tên rõ ràng.\n"
            f"Ngữ cảnh đã ghi nhớ: {memory_context or 'Không có'}\n"
            f"Câu hỏi người dùng: {user_query}\n"
            "Trả lời 4-7 câu, rõ ràng, có thể dùng bullet ngắn nếu cần."
        )
        return call_gemini(prompt) or (
            "Bạn có thể gửi rõ tình huống bán hàng (loại khách, mức giá, sản phẩm muốn đẩy) để mình soạn kịch bản tư vấn/chốt đơn cụ thể hơn."
        )

    prompt = (
        "Bạn là trợ lý FRUIT_SHOP. Trả lời tiếng Việt tự nhiên, mềm mại, không cứng nhắc, có chút chiều khách và khen nhẹ khi hợp ngữ cảnh.\n"
        "Nếu câu hỏi liên quan đến tư vấn mua trái cây/giỏ quà/hộp quà thì định hướng người dùng về tiêu chí chọn (loại, ngân sách, mục đích).\n"
        f"Ngữ cảnh đã ghi nhớ trong phiên: {memory_context or 'Không có'}\n"
        "Nếu câu hỏi vượt ngoài phạm vi cửa hàng, hãy cảm ơn và mời liên hệ trực tiếp theo thông tin này:\n"
        f"{FRUIT_SHOP_CONTACT}\n"
        f"Câu hỏi người dùng: {user_query}\n"
        "Trả lời ngắn gọn 2-4 câu."
    )
    return call_gemini(prompt) or fallback


def search_products_by_constraints(user_query: str, limit: int = 3, forced_type: str = None):
    if not PRODUCTS_CACHE:
        return [], False

    min_price, max_price = parse_price_constraints(user_query)
    preference_tokens = extract_preference_tokens(user_query)

    scored = []
    sale_only = is_sale_query(user_query)
    for index, product in enumerate(PRODUCTS_CACHE):
        if not is_matching_product_type(product, forced_type):
            continue
        if sale_only and not is_on_sale_product(product):
            continue

        price = float(product["final_price"] or 0)
        if min_price is not None and price < min_price:
            continue
        if max_price is not None and price > max_price:
            continue

        blob = product["search_blob"]
        match_count = sum(1 for token in preference_tokens if token_matches_blob(token, blob))
        if preference_tokens and match_count == 0:
            continue

        scored.append((match_count, -price, index, product))

    has_constraints = bool(preference_tokens) or min_price is not None or max_price is not None or bool(forced_type)
    if not scored:
        return [], has_constraints

    scored.sort(key=lambda item: (-item[0], item[1], item[2]))
    picked = [item[3] for item in scored[:limit]]

    results = []
    for product in picked:
        results.append(to_product_response(product))
    return results, has_constraints


def load_products_cache(limit: int = 500):
    global PRODUCTS_CACHE, PRODUCTS_BY_ID

    conn = None
    try:
        conn = pyodbc.connect(SQL_CONN_STR)
        cursor = conn.cursor()
        rows = None
        for original_expr in ("p.price", "p.base_price", "p.original_price", "p.final_price"):
            try:
                cursor.execute(
                    f"""
                    SELECT TOP {int(limit)}
                        p.id,
                        p.name,
                        p.description,
                        p.final_price,
                        {original_expr} AS original_price,
                        p.stock_quantity,
                        p.unit,
                        (
                            SELECT COUNT(1)
                            FROM dbo.reviews r
                            WHERE r.product_id = p.id AND r.status = 1
                        ) AS rating_count,
                        (
                            SELECT CAST(ROUND(AVG(CAST(r.rating AS FLOAT)), 2) AS FLOAT)
                            FROM dbo.reviews r
                            WHERE r.product_id = p.id AND r.status = 1
                        ) AS average_rating,
                        (
                            SELECT TOP 1 pi.image_url
                            FROM dbo.product_images pi
                            WHERE pi.product_id = p.id
                            ORDER BY pi.is_main DESC, pi.id ASC
                        ) AS image_url
                    FROM dbo.products p
                    WHERE p.status = 1 OR p.status IS NULL
                    ORDER BY p.id DESC
                    """
                )
                rows = cursor.fetchall()
                break
            except Exception:
                rows = None

        if rows is None:
            raise RuntimeError("Unable to load products with available price columns")

        products = []
        products_by_id = {}
        for row in rows:
            name = str(row.name or "")
            desc = str(row.description or "")
            unit = str(row.unit or "")
            product = {
                "id": int(row.id),
                "name": name,
                "description": desc,
                "final_price": float(row.final_price or 0),
                "stock_quantity": int(row.stock_quantity or 0),
                "unit": unit,
                "rating_count": int(row.rating_count or 0),
                "average_rating": float(row.average_rating) if row.average_rating is not None else None,
                "image_url": row.image_url,
                "buy_url": f"/buy/{int(row.id)}",
                "search_blob": strip_accents(f"{name} {desc} {unit}"),
            }
            raw_original_price = float(row.original_price or 0)
            product["original_price"] = raw_original_price if raw_original_price > product["final_price"] else None
            product["product_type"] = infer_product_type_from_blob(product["search_blob"], unit)
            products.append(product)
            products_by_id[product["id"]] = product

        PRODUCTS_CACHE = products
        PRODUCTS_BY_ID = products_by_id
        build_related_product_index(PRODUCTS_CACHE)
        sync_vector_index(PRODUCTS_CACHE)
    except Exception:
        PRODUCTS_CACHE = []
        PRODUCTS_BY_ID = {}
    finally:
        if conn is not None:
            conn.close()


@app.on_event("startup")
def startup_load_cache():
    init_vector_store()
    load_products_cache()


@app.get("/")
async def serve_index():
    return FileResponse(os.path.join(BASE_DIR, "index.html"), media_type="text/html")


@app.get("/buy/{product_id}")
async def serve_buy_page(product_id: int):
    return FileResponse(os.path.join(BASE_DIR, "buy.html"), media_type="text/html")


@app.get("/api/products")
async def api_products():
    return {
        "products": [
            {
                key: (svg_placeholder(value) if key == "image_url" and not value else value)
                for key, value in product.items()
                if key != "search_blob"
            }
            for product in PRODUCTS_CACHE[:8]
        ]
    }


@app.get("/api/products/{product_id}")
async def api_product_detail(product_id: int):
    product = PRODUCTS_BY_ID.get(product_id)
    if not product:
        raise HTTPException(status_code=404, detail="Product not found")
    response = {key: value for key, value in product.items() if key != "search_blob"}
    if not response.get("image_url"):
        response["image_url"] = svg_placeholder(response["name"])
    return response


@app.get("/chat/system")
async def chat_system_status():
    return {
        "vector_ready": VECTOR_READY,
        "vector_collection": VECTOR_COLLECTION_NAME,
        "vector_last_error": VECTOR_LAST_ERROR,
        "cached_products": len(PRODUCTS_CACHE),
        "feedback_count": len(FEEDBACK_LOG),
    }


def search_products(user_query: str, limit: int = 3, forced_type: str = None, must_include_token: str = None):
    query = strip_accents(user_query)
    tokens = extract_preference_tokens(user_query)
    sale_only = is_sale_query(user_query)

    if not PRODUCTS_CACHE:
        return []

    scored = []
    for index, product in enumerate(PRODUCTS_CACHE):
        if not is_matching_product_type(product, forced_type):
            continue
        if sale_only and not is_on_sale_product(product):
            continue
        if must_include_token and must_include_token not in set(tokenize(product.get("name") or "")):
            continue

        blob = product["search_blob"]
        score = 0
        if query and query in blob:
            score += 8
        for token in tokens:
            if token_matches_blob(token, blob):
                score += 2
        scored.append((score, index, product))

    scored.sort(key=lambda item: (-item[0], item[1]))
    picked = [item[2] for item in scored[:limit] if item[0] > 0]
    if not picked:
        related_items = get_related_products(
            reference_product=None,
            limit=limit,
            forced_type=forced_type,
            must_include_token=must_include_token,
        )
        picked = [PRODUCTS_BY_ID.get(int(item.get("id") or 0)) for item in related_items]
        picked = [item for item in picked if item]

    results = []
    for product in picked:
        results.append(to_product_response(product))
    return results


def is_online():
    """Check if the system is online by trying to reach Dify API or a reliable host."""
    import ssl
    ctx = ssl.create_default_context()
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    
    # Try multiple hosts to be sure
    hosts = ["https://api.dify.ai", "https://www.google.com", "https://1.1.1.1"]
    for host in hosts:
        try:
            urlrequest.urlopen(host, timeout=3, context=ctx)
            print(f"Network check: Online via {host}")
            return True
        except Exception as e:
            print(f"Network check: Failed to reach {host} ({type(e).__name__})")
            continue
    return False


def call_dify(query: str, session_id: str, conversation_id: str = None):
    if not DIFY_API_KEY or not str(DIFY_API_KEY).strip():
        print("Dify Error: DIFY_API_KEY is empty or not set in config_private.py")
        return None, None, []
    
    api_key = str(DIFY_API_KEY).strip()
    
    endpoints = [
        (f"{DIFY_API_URL}/chat-messages", {
            "inputs": {"user_name": session_id},
            "query": query,
            "response_mode": "blocking",
            "conversation_id": conversation_id or "",
            "user": session_id,
        }),
        (f"{DIFY_API_URL}/workflows/run", {
            "inputs": {"query": query, "user_name": session_id},
            "response_mode": "blocking",
            "user": session_id,
        })
    ]
    
    headers = {
        "Authorization": f"Bearer {api_key}",
        "Content-Type": "application/json",
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
    }
    
    import ssl
    ctx = ssl.create_default_context()
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE

    for endpoint, payload in endpoints:
        try:
            print(f"Calling Dify: {endpoint}")
            req = urlrequest.Request(
                endpoint,
                data=json.dumps(payload).encode("utf-8"),
                headers=headers,
                method="POST"
            )
            with urlrequest.urlopen(req, timeout=35, context=ctx) as resp:
                body = json.loads(resp.read().decode("utf-8"))
                print(f"Dify Response: Success from {endpoint}")
                
                if "answer" in body:
                    answer = body.get("answer", "")
                    new_conv_id = body.get("conversation_id")
                elif "data" in body and "outputs" in body["data"]:
                    answer = body["data"]["outputs"].get("text") or body["data"]["outputs"].get("answer") or ""
                    new_conv_id = None
                else:
                    print(f"Dify Warning: Unexpected response format from {endpoint}: {list(body.keys())}")
                    continue
                
                products = []
                metadata = body.get("metadata", {})
                tool_outputs = metadata.get("tool_outputs", [])
                for tool in tool_outputs:
                    tool_output_data = tool.get("output", {}) if isinstance(tool, dict) else {}
                    if isinstance(tool_output_data, dict) and "products" in tool_output_data:
                        products.extend(tool_output_data["products"])
                
                if not products and "data" in body:
                    outputs = body["data"].get("outputs", {})
                    if "products" in outputs:
                        products = outputs["products"]

                if not products and answer:
                    found_products = []
                    bold_names = re.findall(r"\*\*(.*?)\*\*", answer)
                    answer_clean = strip_accents(answer)
                    
                    for bn in bold_names:
                        bn_clean = strip_accents(bn).strip()
                        if len(bn_clean) < 2: continue
                        for p in PRODUCTS_CACHE:
                            p_name_clean = strip_accents(p["name"]).strip()
                            if bn_clean in p_name_clean or p_name_clean in bn_clean:
                                if not any(item["id"] == p["id"] for item in found_products):
                                    found_products.append(to_product_response(p))
                    
                    if len(found_products) < 3:
                        for p in PRODUCTS_CACHE:
                            p_name_clean = strip_accents(p["name"]).strip()
                            if len(p_name_clean) > 5 and p_name_clean in answer_clean:
                                if not any(item["id"] == p["id"] for item in found_products):
                                    found_products.append(to_product_response(p))
                    
                    if found_products:
                        products = found_products[:4]

                if not products:
                    products = search_products(query, limit=3)
                    
                return answer, new_conv_id, products
                
        except Exception as e:
            print(f"Dify Error at {endpoint}: {type(e).__name__} - {str(e)}")
            continue
            
    return None, None, []


@app.post("/chat")
async def chat_with_ai(request: Request):
    data = await request.json()
    user_query_raw = (data.get("user_query") or "").strip()
    session_id = str(data.get("session_id") or "__default__")
    session_state = CONVERSATION_STATE.setdefault(
        session_id,
        {
            "dify_conversation_id": None,
            "preference_tokens": [],
            "preferred_type": None,
            "last_entities": [],
            "last_results": [],
            "last_intent": None,
            "history": [],
            "chat_history": [],
            "memory": {
                "min_price": None,
                "max_price": None,
                "purpose": None,
                "recipient": None,
                "must_have_tokens": [],
                "avoid_tokens": [],
            },
        },
    )
    if not user_query_raw:
        return {"answer": "Vui lòng nhập câu hỏi.", "products": []}

    # THỰC HIỆN KẾT NỐI DIFY AI NẾU CÓ MẠNG
    if is_online():
        dify_conv_id = session_state.get("dify_conversation_id")
        answer, new_conv_id, products = call_dify(user_query_raw, session_id, dify_conv_id)
        if answer:
            if new_conv_id:
                session_state["dify_conversation_id"] = new_conv_id
            
            # Đồng bộ kết quả để UI hiển thị các sản phẩm liên quan
            if products:
                # Chuyển đổi sang định dạng response nếu là sản phẩm thô từ cache
                processed_products = [
                    to_product_response(p) if "search_blob" in p else p 
                    for p in products
                ]
                session_state["last_results"] = processed_products[:3]
                
                # Cập nhật intent và type để các câu hỏi sau (nếu offline) vẫn có ngữ cảnh
                session_state["last_intent"] = "shopping"
                if processed_products:
                    session_state["preferred_type"] = processed_products[0].get("product_type")

            response_id = str(uuid.uuid4())
            append_chat_turn(session_state, "user", user_query_raw)
            return finalize_chat_response(session_state, {
                "answer": answer,
                "products": [to_product_response(p) if "search_blob" in p else p for p in products],
                "source_products": [item["name"] for item in products],
                "confidence": 0.95 if products else 0.8,
                "response_id": response_id,
                "answer_mode": "advice",
            })

    # FALLBACK: SỬ DỤNG LOGIC CHATBOT LOCAL NẾU MẤT MẠNG HOẶC DIFY LỖI
    user_query = normalize_user_query(user_query_raw)
    if not user_query:
        return {"answer": "Mình chưa đọc rõ ý của bạn, bạn nhắn lại ngắn gọn giúp mình nhé.", "products": []}

    append_chat_turn(session_state, "user", user_query_raw, {"normalized": user_query})

    requested_type = detect_product_type_from_text(user_query)
    if is_fresh_fruit_request(user_query):
        requested_type = "trai_cay_kg"
    if is_negated_gift_request(user_query) and requested_type != "gio_qua":
        session_state["preferred_type"] = None
    if requested_type:
        session_state["preferred_type"] = requested_type
    explicit_fruit_query = is_explicit_fruit_query(user_query)
    active_type = requested_type or (None if explicit_fruit_query else session_state.get("preferred_type"))

    rewritten_query, rewritten_tokens, entities = rewrite_query(user_query, session_state)
    answer_mode = detect_answer_mode(user_query)
    min_price, max_price = parse_price_constraints(rewritten_query)
    memory = update_conversation_memory(
        session_state,
        user_query,
        rewritten_tokens,
        min_price=min_price,
        max_price=max_price,
    )
    rewritten_query, rewritten_tokens, min_price, max_price, memory = apply_memory_to_query(
        session_state,
        rewritten_query,
        rewritten_tokens,
        min_price,
        max_price,
    )
    memory_context = build_conversation_context_snapshot(session_state, memory, user_query_raw)
    sale_only = is_sale_query(rewritten_query)
    intent = classify_intent(user_query)
    requested_rank = parse_requested_rank(user_query)
    previous_results = session_state.get("last_results") or []

    context_type = active_type
    if not context_type and previous_results:
        context_type = previous_results[0].get("product_type")

    same_type_request = is_same_type_request(user_query)
    same_type = active_type or context_type
    query_fruit = extract_primary_fruit_token(user_query)
    fruit_context = resolve_fruit_context(user_query, previous_results=previous_results, memory=memory)
    same_fruit = fruit_context
    memory_context = build_conversation_context_snapshot(session_state, memory, user_query_raw)

    if explicit_fruit_query and same_type != "trai_cay_kg":
        same_type = "trai_cay_kg"

    def remember_results(products, intent: str = "shopping"):
        if products:
            session_state["last_results"] = products[:3]
            session_state["last_intent"] = intent
            inferred_type = products[0].get("product_type")
            if inferred_type:
                session_state["preferred_type"] = inferred_type
            session_state["last_product_name"] = products[0].get("name") or session_state.get("last_product_name")
            session_state["last_fruit_token"] = extract_product_fruit_token(products[0]) or session_state.get("last_fruit_token")

    if previous_results and is_buy_decision_query(user_query):
        rank_index = max(0, (requested_rank or 1) - 1)
        selected_item = previous_results[min(rank_index, len(previous_results) - 1)]
        remember_results([selected_item])
        return finalize_chat_response(session_state, {
            "answer": build_intent_reply_with_llm(
                user_query_raw,
                "buy_decision",
                [selected_item],
                memory_context=memory_context,
                forced_type=same_type or active_type,
            ),
            "products": [selected_item],
            "source_products": [selected_item["name"]],
            "confidence": 0.92,
            "answer_mode": "advice",
        })

    referenced_product = resolve_referenced_product(
        user_query,
        previous_results,
        preferred_type=("trai_cay_kg" if explicit_fruit_query else active_type),
    )
    if referenced_product and "search_blob" in referenced_product:
        referenced_product = to_product_response(referenced_product)
    if not context_type and referenced_product:
        context_type = referenced_product.get("product_type")

    if same_type_request:
        same_type = (
            (referenced_product or {}).get("product_type")
            or context_type
            or active_type
        )
        if not fruit_context:
            fruit_context = extract_product_fruit_token(referenced_product)
        if not fruit_context and previous_results:
            for item in previous_results:
                fruit_context = extract_product_fruit_token(item)
                if fruit_context:
                    break

    if explicit_fruit_query and query_fruit:
        fruit_context = query_fruit

    same_fruit = fruit_context

    visible_ranked_product = resolve_ranked_visible_product(user_query, previous_results)
    if visible_ranked_product:
        remember_results([visible_ranked_product])
        if is_contextual_product_followup(user_query) or parse_requested_rank(user_query) or is_deeper_product_followup_query(user_query):
            return finalize_chat_response(session_state, {
                "answer": build_dynamic_answer_with_llm(
                    user_query_raw,
                    "info",
                    [visible_ranked_product],
                    confidence=0.94,
                    prefer_cheaper=False,
                    memory_context=memory_context,
                ),
                "products": [visible_ranked_product],
                "source_products": [visible_ranked_product["name"]],
                "confidence": 0.94,
                "answer_mode": "info",
                "display_products": False,
            })

    if answer_mode == "compare":
        compare_products = resolve_compare_products(
            user_query,
            previous_results=previous_results,
            preferred_type=same_type or active_type,
            base_product=referenced_product,
        )
        if len(compare_products) >= 2:
            remember_results(compare_products, intent="compare")
            return finalize_chat_response(session_state, {
                "answer": build_compare_reply(compare_products, user_query_raw),
                "products": compare_products[:2],
                "source_products": [item["name"] for item in compare_products[:2]],
                "confidence": 0.93,
                "answer_mode": "compare",
            })

        if compare_products:
            remember_results(compare_products, intent="compare")
            return finalize_chat_response(session_state, {
                "answer": build_compare_reply(compare_products, user_query_raw),
                "products": compare_products,
                "source_products": [item["name"] for item in compare_products],
                "confidence": 0.55,
                "answer_mode": "compare",
            })

        return finalize_chat_response(session_state, {
            "answer": add_contact_info("Mình chưa xác nhận đủ 2 sản phẩm để so sánh. Bạn gửi rõ tên 2 sản phẩm giúp mình nhé, ví dụ: A với B."),
            "products": [],
            "confidence": 0.35,
            "answer_mode": "compare",
        })

    if session_state.get("last_intent") == "compare" and previous_results and is_buy_decision_query(user_query):
        compare_products = previous_results[:2]
        if len(compare_products) >= 2:
            return finalize_chat_response(session_state, {
                "answer": build_compare_reply(compare_products, user_query_raw),
                "products": compare_products,
                "source_products": [item["name"] for item in compare_products],
                "confidence": 0.9,
                "answer_mode": "compare",
            })

    if is_availability_query(user_query):
        if referenced_product:
            remember_results([referenced_product])
            return finalize_chat_response(session_state, {
                "answer": build_intent_reply_with_llm(
                    user_query_raw,
                    "availability",
                    [referenced_product],
                    memory_context=memory_context,
                    forced_type=same_type or active_type,
                ),
                "products": [referenced_product],
                "source_products": [referenced_product["name"]],
                "confidence": 0.9,
                "answer_mode": "info",
            })

        quick_options = get_alternative_products(limit=3, forced_type=same_type or active_type, must_include_token=fruit_context, reference_product=referenced_product)
        remember_results(quick_options)
        return finalize_chat_response(session_state, {
            "answer": build_intent_reply_with_llm(
                user_query_raw,
                "availability",
                quick_options,
                memory_context=memory_context,
                forced_type=same_type or active_type,
            ),
            "products": quick_options,
            "source_products": [item["name"] for item in quick_options],
            "confidence": 0.82,
            "answer_mode": "advice",
        })

    if referenced_product and is_buy_decision_query(user_query):
        remember_results([referenced_product])
        return finalize_chat_response(session_state, {
            "answer": build_intent_reply_with_llm(
                user_query_raw,
                "buy_decision",
                [referenced_product],
                memory_context=memory_context,
                forced_type=same_type or active_type,
            ),
            "products": [referenced_product],
            "source_products": [referenced_product["name"]],
            "confidence": 0.92,
            "answer_mode": "advice",
        })

    if referenced_product and is_deeper_product_followup_query(user_query):
        remember_results([referenced_product])
        return finalize_chat_response(session_state, {
            "answer": build_deeper_product_followup_with_llm(
                user_query_raw,
                referenced_product,
                memory_context=memory_context,
            ),
            "products": [referenced_product],
            "source_products": [referenced_product["name"]],
            "confidence": 0.9,
            "answer_mode": "info",
            "display_products": False,
        })

    if referenced_product and (is_contextual_product_followup(user_query) or requested_rank or strip_accents(referenced_product.get("name") or "") in user_query):
        remember_results([referenced_product])
        return finalize_chat_response(session_state, {
            "answer": build_dynamic_answer_with_llm(
                user_query_raw,
                "info",
                [referenced_product],
                confidence=0.9,
                prefer_cheaper=False,
                memory_context=memory_context,
            ),
            "products": [referenced_product],
            "source_products": [referenced_product["name"]],
            "confidence": 0.9,
            "answer_mode": "info",
            "display_products": False,
        })

    if previous_results and is_contextual_product_followup(user_query):
        rank_index = max(0, (requested_rank or 1) - 1)
        selected_item = previous_results[min(rank_index, len(previous_results) - 1)]
        remember_results([selected_item])
        return finalize_chat_response(session_state, {
            "answer": build_dynamic_answer_with_llm(
                user_query_raw,
                "info",
                [selected_item],
                confidence=0.9,
                prefer_cheaper=False,
                memory_context=memory_context,
            ),
            "products": [selected_item],
            "source_products": [selected_item["name"]],
            "confidence": 0.9,
            "answer_mode": "info",
            "display_products": False,
        })

    if previous_results and is_deeper_product_followup_query(user_query):
        rank_index = max(0, (requested_rank or 1) - 1)
        selected_item = previous_results[min(rank_index, len(previous_results) - 1)]
        remember_results([selected_item])
        return finalize_chat_response(session_state, {
            "answer": build_deeper_product_followup_with_llm(
                user_query_raw,
                selected_item,
                memory_context=memory_context,
            ),
            "products": [selected_item],
            "source_products": [selected_item["name"]],
            "confidence": 0.88,
            "answer_mode": "info",
            "display_products": False,
        })

    business_sales_mode = is_business_sales_query(user_query)

    if business_sales_mode:
        return finalize_chat_response(session_state, {
            "answer": build_general_chat_answer_with_llm(
                user_query_raw,
                sales_coach_mode=True,
                memory_context=memory_context,
            ),
            "products": [],
            "intent": "chat",
        })

    if answer_mode == "info" and requested_rank:
        rank_index = max(0, requested_rank - 1)
        prev_results = session_state.get("last_results") or []

        # Use previous ranked list first when user asks "thứ nhất/thứ hai..."
        if rank_index < len(prev_results):
            selected_item = prev_results[rank_index]
            answer = build_dynamic_answer_with_llm(
                user_query_raw,
                "info",
                [selected_item],
                confidence=0.9,
                prefer_cheaper=False,
                memory_context=memory_context,
            )
            remember_results([selected_item])
            return finalize_chat_response(session_state, {
                "answer": answer,
                "products": [selected_item],
                "source_products": [selected_item["name"]],
                "confidence": 0.9,
                "answer_mode": "info",
            })

        # If no previous results, retrieve then pick by rank.
        candidates = hybrid_retrieve(
            rewritten_query,
            rewritten_tokens,
            forced_type=same_type or active_type,
            sale_only=sale_only,
            min_price=min_price,
            max_price=max_price,
            top_k=10,
        )
        reranked = rerank_candidates(candidates, rewritten_tokens, "info", prefer_cheaper=False)
        selected_by_rank = [to_product_response(item["product"]) for item in reranked]
        if rank_index < len(selected_by_rank):
            selected_item = selected_by_rank[rank_index]
            remember_results([selected_item])
            return finalize_chat_response(session_state, {
                "answer": build_dynamic_answer_with_llm(
                    user_query_raw,
                    "info",
                    [selected_item],
                    confidence=estimate_confidence(reranked, has_constraints=True),
                    prefer_cheaper=False,
                    memory_context=memory_context,
                ),
                "products": [selected_item],
                "source_products": [selected_item["name"]],
                "confidence": round(estimate_confidence(reranked, has_constraints=True), 2),
                "answer_mode": "info",
            })

    if is_asking_cheaper(user_query):
        previous_prices = [float(item.get("final_price") or 0) for item in session_state.get("last_results") or [] if item.get("final_price")]
        if previous_prices and max_price is None:
            max_price = max(1.0, min(previous_prices) - 1)

        candidates = hybrid_retrieve(
            rewritten_query,
            rewritten_tokens,
            forced_type=same_type or active_type,
            sale_only=sale_only,
            min_price=min_price,
            max_price=max_price,
            top_k=10,
        )
        reranked = rerank_candidates(candidates, rewritten_tokens, answer_mode, prefer_cheaper=True)
        selected = [to_product_response(item["product"]) for item in reranked[:3]]
        confidence = estimate_confidence(reranked, has_constraints=True)

        if not selected:
            suggest_gift_baskets = bool(min_price is not None or max_price is not None) and (not active_type or active_type in {"gio_qua", "hop_qua"})
            return finalize_chat_response(session_state, {
                "answer": build_no_match_reply(active_type or same_type, min_price, max_price, suggest_gift_baskets=suggest_gift_baskets),
                "products": [],
                "confidence": round(confidence, 2),
            })

        answer = build_dynamic_answer_with_llm(
            user_query_raw,
            answer_mode,
            selected,
            confidence,
            prefer_cheaper=True,
            memory_context=memory_context,
        )
        response_id = str(uuid.uuid4())
        session_state["last_entities"] = entities or session_state.get("last_entities") or []
        session_state["preference_tokens"] = rewritten_tokens
        remember_results(selected)
        session_state["last_intent"] = "shopping"
        session_state["history"].append({
            "response_id": response_id,
            "user_query": user_query_raw,
            "normalized_user_query": user_query,
            "rewritten_query": rewritten_query,
            "intent": "shopping",
            "answer_mode": answer_mode,
            "confidence": round(confidence, 3),
            "timestamp": datetime.utcnow().isoformat(),
        })
        session_state["history"] = session_state["history"]

        return finalize_chat_response(session_state, {
            "answer": answer,
            "products": selected,
            "source_products": [item["name"] for item in selected],
            "confidence": round(confidence, 2),
            "response_id": response_id,
            "answer_mode": answer_mode,
        })

    if is_rejecting_recommendation(user_query):
        products = get_alternative_products(limit=3, forced_type=same_type or context_type, must_include_token=fruit_context, reference_product=previous_results[0] if previous_results else referenced_product)
        if not products:
            return finalize_chat_response(session_state, {
                "answer": add_contact_info("Ok, vậy mình sẽ gợi ý sản phẩm khác cho bạn ngay khi có dữ liệu nhé."),
                "products": [],
            })
        remember_results(products)
        return finalize_chat_response(session_state, {
            "answer": build_intent_reply_with_llm(
                user_query_raw,
                "alternative",
                products,
                memory_context=memory_context,
                forced_type=same_type or context_type,
            ),
            "products": products,
            "source_products": [item["name"] for item in products],
        })

    if is_contextual_shopping_followup(user_query, has_previous_results=bool(previous_results)):
        base_product = referenced_product or (previous_results[0] if previous_results else None)
        gift_mode = (same_type in {"gio_qua", "hop_qua"}) or has_gift_marker(user_query)
        if gift_mode and base_product:
            related_products = get_alternative_products(
                limit=2,
                forced_type=same_type or context_type,
                must_include_token=fruit_context,
                reference_product=base_product,
            )
            products = [base_product] + [item for item in related_products if item.get("id") != base_product.get("id")]
        else:
            products = get_alternative_products(limit=3, forced_type=same_type or context_type, must_include_token=fruit_context, reference_product=base_product)
        if products:
            remember_results(products)
            response_mode = "compare" if gift_mode and len(products) > 1 else "advice"
            if response_mode == "compare":
                return finalize_chat_response(session_state, {
                    "answer": build_dynamic_answer_with_llm(
                        user_query_raw,
                        "compare",
                        products,
                        confidence=0.88,
                        prefer_cheaper=False,
                        memory_context=memory_context,
                    ),
                    "products": products,
                    "source_products": [item["name"] for item in products],
                    "answer_mode": response_mode,
                })
            return finalize_chat_response(session_state, {
                "answer": build_intent_reply_with_llm(
                    user_query_raw,
                    "same_group",
                    products,
                    memory_context=memory_context,
                    forced_type=same_type or context_type,
                ),
                "products": products,
                "source_products": [item["name"] for item in products],
                "answer_mode": response_mode,
            })

    if intent == "greeting":
        return finalize_chat_response(session_state, {
            "answer": "Chào bạn. Bạn muốn mình tư vấn theo tiêu chí nào: loại trái cây, tầm giá, hay mục đích dùng (ăn liền, ép nước, làm quà)?",
            "products": [],
        })

    if intent == "chat":
        if is_smalltalk_query(user_query):
            return finalize_chat_response(session_state, {
                "answer": build_chat_reply(user_query_raw),
                "products": [],
            })

        if active_type and is_contextual_product_followup(user_query):
            products = search_products(user_query, limit=3, forced_type=same_type or active_type, must_include_token=fruit_context)
            if products:
                confidence = 0.78
                remember_results(products)
                return finalize_chat_response(session_state, {
                    "answer": build_dynamic_answer_with_llm(
                        user_query_raw,
                        "info",
                        products,
                        confidence=confidence,
                        prefer_cheaper=False,
                        memory_context=memory_context,
                    ),
                    "products": products,
                    "source_products": [item["name"] for item in products],
                    "confidence": confidence,
                    "answer_mode": "info",
                })

        return finalize_chat_response(session_state, {
            "answer": build_general_chat_answer_with_llm(user_query_raw, memory_context=memory_context),
            "products": [],
        })

    has_constraints = bool(min_price is not None or max_price is not None or active_type or sale_only or rewritten_tokens)
    candidates = hybrid_retrieve(
        rewritten_query,
        rewritten_tokens,
        forced_type=same_type or active_type,
        sale_only=sale_only,
        min_price=min_price,
        max_price=max_price,
        top_k=10,
    )
    reranked = rerank_candidates(candidates, rewritten_tokens, answer_mode, prefer_cheaper=False)
    selected = [to_product_response(item["product"]) for item in reranked[:3]]
    confidence = estimate_confidence(reranked, has_constraints=has_constraints)

    if not selected:
        alternatives = get_alternative_products(limit=3, forced_type=same_type or active_type, must_include_token=fruit_context, reference_product=previous_results[0] if previous_results else referenced_product)
        if alternatives:
            suggest_gift_baskets = bool(min_price is not None or max_price is not None) and (not active_type or active_type in {"gio_qua", "hop_qua"} or same_type in {"gio_qua", "hop_qua"})
            return finalize_chat_response(session_state, {
                "answer": build_no_match_reply(same_type or active_type, min_price, max_price, suggest_gift_baskets=suggest_gift_baskets),
                "products": alternatives,
                "source_products": [item["name"] for item in alternatives],
                "confidence": round(confidence, 2),
            })
        return finalize_chat_response(session_state, {
            "answer": add_contact_info(
                "Mình chưa tìm thấy dữ liệu phù hợp trong kho hiện tại. "
                "Bạn có thể cho mình thêm tiêu chí để lọc chính xác hơn nhé."
            ),
            "products": [],
            "confidence": round(confidence, 2),
        })

    answer = build_dynamic_answer_with_llm(
        user_query_raw,
        answer_mode,
        selected,
        confidence,
        prefer_cheaper=False,
        memory_context=memory_context,
    )
    response_id = str(uuid.uuid4())
    session_state["last_entities"] = entities
    session_state["preference_tokens"] = rewritten_tokens
    remember_results(selected)
    session_state["last_intent"] = "shopping"
    session_state["history"].append({
        "response_id": response_id,
        "user_query": user_query_raw,
        "normalized_user_query": user_query,
        "rewritten_query": rewritten_query,
        "intent": "shopping",
        "answer_mode": answer_mode,
        "confidence": round(confidence, 3),
        "timestamp": datetime.utcnow().isoformat(),
    })
    session_state["history"] = session_state["history"]

    return finalize_chat_response(session_state, {
        "answer": answer,
        "products": selected,
        "source_products": [item["name"] for item in selected],
        "confidence": round(confidence, 2),
        "response_id": response_id,
        "answer_mode": answer_mode,
    })


@app.post("/chat/feedback")
async def chat_feedback(request: Request):
    data = await request.json()
    session_id = str(data.get("session_id") or "__default__")
    response_id = str(data.get("response_id") or "").strip()
    feedback = str(data.get("feedback") or "").strip().lower()
    note = str(data.get("note") or "").strip()

    if feedback not in {"up", "down"}:
        raise HTTPException(status_code=400, detail="feedback must be 'up' or 'down'")

    record = {
        "id": str(uuid.uuid4()),
        "session_id": session_id,
        "response_id": response_id,
        "feedback": feedback,
        "note": note,
        "timestamp": datetime.utcnow().isoformat(),
    }
    FEEDBACK_LOG.append(record)
    if len(FEEDBACK_LOG) > 2000:
        del FEEDBACK_LOG[:500]

    return {"ok": True}


@app.post("/chat/reset")
async def chat_reset(request: Request):
    data = await request.json()
    session_id = str(data.get("session_id") or "__default__").strip()
    if not session_id:
        raise HTTPException(status_code=400, detail="session_id is required")

    reset_conversation_state(session_id)
    return {"ok": True}


if __name__ == "__main__":
    import uvicorn
    import socket

    def find_free_port(start_port: int = 8000, max_tries: int = 20) -> int:
        for port in range(start_port, start_port + max_tries):
            with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
                sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
                if sock.connect_ex(("127.0.0.1", port)) != 0:
                    return port
        return start_port

    port = find_free_port(int(os.getenv("PORT", "8000")))
    print(f"Starting server on http://127.0.0.1:{port}")

    uvicorn.run(app, host="0.0.0.0", port=port)
