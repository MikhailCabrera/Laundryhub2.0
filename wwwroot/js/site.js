/**
 * Laundry Hub Davao City - Interactive Application Logic
 * Modal management, live pricing calculator, district coverage checker, form validations.
 */

document.addEventListener('DOMContentLoaded', () => {
    // ------------------------------------------------------------------------
    // 1. Navigation & Sticky Header Behavior
    // ------------------------------------------------------------------------
    const navbar = document.getElementById('navbar');
    const menuToggle = document.getElementById('menuToggle');
    const navMenu = document.getElementById('navMenu');

    window.addEventListener('scroll', () => {
        if (window.scrollY > 20) {
            navbar.classList.add('scrolled');
        } else {
            navbar.classList.remove('scrolled');
        }
    });

    if (menuToggle && navMenu) {
        menuToggle.addEventListener('click', () => {
            const isOpen = navMenu.classList.toggle('is-open');
            menuToggle.classList.toggle('active', isOpen);
            menuToggle.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
        });

        // Close mobile menu on clicking any navigation link
        navMenu.querySelectorAll('.lh-nav-link').forEach(link => {
            link.addEventListener('click', () => {
                navMenu.classList.remove('is-open');
                menuToggle.classList.remove('active');
                menuToggle.setAttribute('aria-expanded', 'false');
            });
        });
    }

    // ------------------------------------------------------------------------
    // 2. Signup / Register Triggers
    // ------------------------------------------------------------------------
    // Direct page navigation for signup triggers across the page (Hero, CTA, etc.)
    document.querySelectorAll('[data-trigger-signup]').forEach(btn => {
        btn.addEventListener('click', () => {
            window.location.href = '/Account/Register';
        });
    });

    // ------------------------------------------------------------------------
    // 4. Interactive Per-Kilo Cost Calculator
    // ------------------------------------------------------------------------
    const serviceSelect = document.getElementById('calcServiceSelect');
    const weightSlider = document.getElementById('weightSlider');
    const weightDisplay = document.getElementById('weightDisplay');
    const expressCheckbox = document.getElementById('expressCheckbox');
    const presetButtons = document.querySelectorAll('.lh-preset-btn');

    const summaryServiceName = document.getElementById('summaryServiceName');
    const summaryServiceTotal = document.getElementById('summaryServiceTotal');
    const summaryExpressLine = document.getElementById('summaryExpressLine');
    const summaryExpressTotal = document.getElementById('summaryExpressTotal');
    const summaryDeliveryFee = document.getElementById('summaryDeliveryFee');
    const freeDeliveryTracker = document.getElementById('freeDeliveryTracker');
    const trackerText = document.getElementById('trackerText');
    const trackerFill = document.getElementById('trackerFill');
    const totalEstimate = document.getElementById('totalEstimate');
    const bookEstimateBtn = document.getElementById('bookEstimateBtn');

    const serviceNames = {
        '45': 'Wash & Fold (₱45/kg)',
        '60': 'Wash & Iron (₱60/kg)',
        '70': 'Comforters & Linens (₱70/kg)',
        '150': 'Dry Cleaning (₱150/kg)'
    };

    function updateCalculator() {
        if (!weightSlider || !serviceSelect) return;

        const weight = parseFloat(weightSlider.value) || 3;
        const ratePerKg = parseFloat(serviceSelect.value) || 45;
        const isExpress = expressCheckbox ? expressCheckbox.checked : false;

        // Display weight
        if (weightDisplay) {
            weightDisplay.textContent = weight.toFixed(weight % 1 === 0 ? 0 : 1);
        }

        // Service cost calculation
        const baseCost = weight * ratePerKg;
        if (summaryServiceName) {
            const rawName = serviceNames[serviceSelect.value] || 'Custom Laundry';
            summaryServiceName.textContent = `${rawName.split('(')[0].trim()} (${weight} kg @ ₱${ratePerKg}/kg)`;
        }
        if (summaryServiceTotal) {
            summaryServiceTotal.textContent = `₱${baseCost.toFixed(2)}`;
        }

        // Express cost calculation (+₱20/kg)
        let expressCost = 0;
        if (isExpress) {
            expressCost = weight * 20;
            if (summaryExpressLine) summaryExpressLine.style.display = 'flex';
            if (summaryExpressTotal) summaryExpressTotal.textContent = `+₱${expressCost.toFixed(2)}`;
        } else {
            if (summaryExpressLine) summaryExpressLine.style.display = 'none';
        }

        // Delivery fee: FREE on 5kg+, ₱50 if below 5kg
        let deliveryFee = 0;
        if (weight >= 5) {
            deliveryFee = 0;
            if (summaryDeliveryFee) {
                summaryDeliveryFee.textContent = 'FREE (5kg+)';
                summaryDeliveryFee.className = 'lh-fee-free';
            }
            if (trackerText) {
                trackerText.innerHTML = '🎉 <strong>FREE</strong> Davao pickup & delivery unlocked!';
            }
            if (trackerFill) {
                trackerFill.style.width = '100%';
            }
        } else {
            deliveryFee = 50;
            const diff = (5 - weight).toFixed(1);
            if (summaryDeliveryFee) {
                summaryDeliveryFee.textContent = '₱50.00';
                summaryDeliveryFee.className = '';
            }
            if (trackerText) {
                trackerText.innerHTML = `Add <strong>${diff} kg</strong> more to unlock FREE Delivery!`;
            }
            if (trackerFill) {
                const percent = Math.min(100, Math.max(10, (weight / 5) * 100));
                trackerFill.style.width = `${percent}%`;
            }
        }

        // Grand Total in Philippine Pesos (₱)
        const grandTotal = baseCost + expressCost + deliveryFee;
        if (totalEstimate) {
            totalEstimate.textContent = `₱${Math.round(grandTotal).toLocaleString('en-PH')}`;
        }
    }

    if (weightSlider) {
        weightSlider.addEventListener('input', () => {
            // Unset preset buttons active states if custom slider moved
            presetButtons.forEach(btn => {
                if (parseFloat(btn.dataset.weight) === parseFloat(weightSlider.value)) {
                    btn.classList.add('active');
                } else {
                    btn.classList.remove('active');
                }
            });
            updateCalculator();
        });
    }

    if (serviceSelect) {
        serviceSelect.addEventListener('change', updateCalculator);
    }

    if (expressCheckbox) {
        expressCheckbox.addEventListener('change', updateCalculator);
    }

    presetButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            presetButtons.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            const targetWeight = btn.dataset.weight;
            if (weightSlider) {
                weightSlider.value = targetWeight;
                updateCalculator();
            }
        });
    });

    // "Calculate This Service" buttons on the pricing cards
    document.querySelectorAll('[data-select-calc]').forEach(btn => {
        btn.addEventListener('click', () => {
            const rate = btn.getAttribute('data-select-calc');
            if (serviceSelect) {
                serviceSelect.value = rate;
                updateCalculator();
            }
            const calcElement = document.getElementById('calculator');
            if (calcElement) {
                calcElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        });
    });

    if (bookEstimateBtn) {
        bookEstimateBtn.addEventListener('click', () => {
            window.location.href = '/Account/Register';
        });
    }

    // Initialize calculator on page load
    updateCalculator();

    // ------------------------------------------------------------------------
    // 5. Davao City Districts Coverage Checker
    // ------------------------------------------------------------------------
    const districtPills = document.querySelectorAll('.lh-district-pill');
    const selectedDistrictName = document.getElementById('selectedDistrictName');
    const selectedDistrictSchedule = document.getElementById('selectedDistrictSchedule');

    districtPills.forEach(pill => {
        pill.addEventListener('click', () => {
            districtPills.forEach(p => p.classList.remove('active'));
            pill.classList.add('active');

            const district = pill.getAttribute('data-district');
            const schedule = pill.getAttribute('data-schedule');

            if (selectedDistrictName) {
                selectedDistrictName.textContent = `${district} District, Davao City`;
            }
            if (selectedDistrictSchedule) {
                selectedDistrictSchedule.innerHTML = `<strong>Pickup & Delivery Schedule:</strong> ${schedule}`;
            }

            // Sync with sign-up modal district dropdown if open
            const signupDistrict = document.getElementById('signupDistrict');
            if (signupDistrict) {
                signupDistrict.value = district;
            }
        });
    });

    // ------------------------------------------------------------------------
    // 6. Global Notification Toast
    // ------------------------------------------------------------------------
    const toast = document.getElementById('lhToast');
    const toastMessage = document.getElementById('toastMessage');
    const toastClose = document.getElementById('toastClose');
    let toastTimeout = null;

    window.showToast = function(message) {
        if (!toast || !toastMessage) return;
        toastMessage.textContent = message;
        toast.hidden = false;

        if (toastTimeout) clearTimeout(toastTimeout);
        toastTimeout = setTimeout(() => {
            toast.hidden = true;
        }, 4500);
    };

    if (toastClose) {
        toastClose.addEventListener('click', () => {
            if (toast) toast.hidden = true;
        });
    }
});
