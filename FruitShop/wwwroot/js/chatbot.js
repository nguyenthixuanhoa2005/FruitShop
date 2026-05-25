/* Chatbot Logic */
(function() {
    const API_URL = 'http://127.0.0.1:8000'; // Đảm bảo Chatbot backend đang chạy ở port này
    let sessionId = localStorage.getItem('chatbot_session_id') || 'session_' + Math.random().toString(36).substr(2, 9);
    localStorage.setItem('chatbot_session_id', sessionId);

    const widgetHtml = `
        <div id="chatbot-widget">
            <div id="chatbot-button">
                <i class="bi bi-chat-dots-fill"></i>
            </div>
            <div id="chatbot-window">
                <div class="chatbot-header">
                    <div class="chatbot-brand">
                        <div class="chatbot-logo">F</div>
                        <span class="chatbot-title">Fruit Shop AI</span>
                    </div>
                    <div class="chatbot-close"><i class="bi bi-x-lg"></i></div>
                </div>
                <div id="chatbot-messages">
                    <div class="chatbot-msg ai">Chào bạn! Mình là trợ lý ảo của Fruit Shop. Mình có thể giúp gì cho bạn hôm nay?</div>
                </div>
                <div class="chatbot-input-area">
                    <input type="text" id="chatbot-input" placeholder="Nhập tin nhắn..." autocomplete="off">
                    <button id="chatbot-send"><i class="bi bi-send-fill"></i></button>
                </div>
            </div>
        </div>
    `;

    document.body.insertAdjacentHTML('beforeend', widgetHtml);

    const button = document.getElementById('chatbot-button');
    const window = document.getElementById('chatbot-window');
    const closeBtn = document.querySelector('.chatbot-close');
    const input = document.getElementById('chatbot-input');
    const sendBtn = document.getElementById('chatbot-send');
    const messagesContainer = document.getElementById('chatbot-messages');

    button.addEventListener('click', () => {
        window.classList.toggle('active');
        if (window.classList.contains('active')) {
            input.focus();
        }
    });

    closeBtn.addEventListener('click', () => {
        window.classList.remove('active');
    });

    function addMessage(text, isAi = false, products = []) {
        const msgDiv = document.createElement('div');
        msgDiv.className = `chatbot-msg ${isAi ? 'ai' : 'user'}`;
        
        // Sử dụng innerHTML để hỗ trợ xuống dòng và các format cơ bản từ AI
        // Lưu ý: Trong thực tế nên dùng thư viện như DOMPurify để sanite HTML nếu dữ liệu từ nguồn không tin cậy
        msgDiv.innerHTML = text.replace(/\n/g, '<br>');
        
        messagesContainer.appendChild(msgDiv);

        if (products && products.length > 0) {
            const cardList = document.createElement('div');
            cardList.className = 'chatbot-card-list';
            
            products.forEach(p => {
                const card = document.createElement('div');
                card.className = 'chatbot-product-card';
                card.setAttribute('role', 'link');
                card.setAttribute('tabindex', '0');
                
                // Format giá
                const priceFormatted = new Intl.NumberFormat('vi-VN').format(p.final_price) + 'đ';
                
                // Tạo link
                const detailUrl = `/Products/Details/${p.id}`;
                const buyUrl = `/Cart/AddToCart/${p.id}`;
                card.dataset.detailUrl = detailUrl;

                card.innerHTML = `
                    <div class="chatbot-product-info">
                        <div class="chatbot-product-name" title="${p.name}">${p.name}</div>
                        <div class="chatbot-price-wrap">
                            <div class="chatbot-product-price">${priceFormatted}</div>
                        </div>
                        <div class="chatbot-actions">
                            <a href="${detailUrl}" class="chatbot-btn chatbot-btn-detail">Chi tiết</a>
                            <a href="${buyUrl}" class="chatbot-btn chatbot-btn-buy">Mua ngay</a>
                        </div>
                    </div>
                `;

                card.addEventListener('click', (event) => {
                    if (event.target.closest('a, button')) {
                        return;
                    }
                    window.location.href = detailUrl;
                });

                card.addEventListener('keydown', (event) => {
                    if (event.key === 'Enter' || event.key === ' ') {
                        event.preventDefault();
                        window.location.href = detailUrl;
                    }
                });

                cardList.appendChild(card);
            });
            messagesContainer.appendChild(cardList);
        }

        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }

    async function sendMessage() {
        const text = input.value.trim();
        if (!text) return;

        addMessage(text, false);
        input.value = '';
        
        const loadingDiv = document.createElement('div');
        loadingDiv.className = 'chatbot-msg ai';
        loadingDiv.innerHTML = '<i>Đang suy nghĩ...</i>';
        messagesContainer.appendChild(loadingDiv);
        messagesContainer.scrollTop = messagesContainer.scrollHeight;

        try {
            const userId = document.getElementById('session-user-id')?.value || null;

            const response = await fetch(`${API_URL}/chat`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    user_query: text,
                    session_id: sessionId,
                    user_id: userId
                })
            });

            loadingDiv.remove();
            const data = await response.json();
            console.log('Chatbot API Response:', data);
            
            if (data.answer) {
                addMessage(data.answer, true, data.products);
            } else {
                addMessage("Xin lỗi, mình gặp chút sự cố. Bạn thử lại nhé!", true);
            }
        } catch (error) {
            if (loadingDiv) loadingDiv.remove();
            console.error('Chatbot Error:', error);
            addMessage("Không thể kết nối với máy chủ chatbot. Vui lòng đảm bảo backend đang chạy.", true);
        }
    }

    sendBtn.addEventListener('click', sendMessage);
    input.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') sendMessage();
    });
})();
