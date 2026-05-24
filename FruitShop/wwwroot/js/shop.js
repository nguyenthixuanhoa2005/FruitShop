document.addEventListener("DOMContentLoaded", function () {
	const slides = document.querySelectorAll(".slide");
	const dots = document.querySelectorAll("#heroDots button");
	const prevBtn = document.querySelector('[data-slide="prev"]');
	const nextBtn = document.querySelector('[data-slide="next"]');

	if (!slides.length) {
		return;
	}

	let activeIndex = 0;
	let autoPlayTimer;

	function setActiveSlide(index) {
		activeIndex = (index + slides.length) % slides.length;

		slides.forEach((slide, i) => {
			slide.classList.toggle("is-active", i === activeIndex);
		});

		dots.forEach((dot, i) => {
			if (i === activeIndex) {
				dot.classList.add("bg-brand");
				dot.classList.remove("bg-white/80");
			} else {
				dot.classList.remove("bg-brand");
				dot.classList.add("bg-white/80");
			}
		});
	}

	function startAutoPlay() {
		stopAutoPlay();
		autoPlayTimer = setInterval(function () {
			setActiveSlide(activeIndex + 1);
		}, 3500);
	}

	function stopAutoPlay() {
		if (autoPlayTimer) {
			clearInterval(autoPlayTimer);
			autoPlayTimer = null;
		}
	}

	dots.forEach((dot) => {
		dot.addEventListener("click", function () {
			setActiveSlide(Number(dot.dataset.index || 0));
			startAutoPlay();
		});
	});

	if (prevBtn) {
		prevBtn.addEventListener("click", function () {
			setActiveSlide(activeIndex - 1);
			startAutoPlay();
		});
	}

	if (nextBtn) {
		nextBtn.addEventListener("click", function () {
			setActiveSlide(activeIndex + 1);
			startAutoPlay();
		});
	}

	const slider = document.querySelector("[data-slider-root]");

	if (slider) {
		slider.addEventListener("mouseenter", stopAutoPlay);
		slider.addEventListener("mouseleave", startAutoPlay);
	}

	setActiveSlide(0);
	startAutoPlay();
});
