(() => {
    'use strict';
    const digits = value => String(value ?? '')
        .replace(/[۰-۹]/g, d => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d)))
        .replace(/[٠-٩]/g, d => String('٠١٢٣٤٥٦٧٨٩'.indexOf(d)));
    const jalali = new Intl.DateTimeFormat('fa-IR-u-ca-persian-nu-latn', {
        year: 'numeric', month: '2-digit', day: '2-digit', timeZone: 'UTC'
    });
    const partsFor = date => Object.fromEntries(
        jalali.formatToParts(date).filter(x => x.type !== 'literal').map(x => [x.type, Number(x.value)])
    );
    const isoParts = value => {
        const m = String(value || '').match(/^(\d{4})-(\d{2})-(\d{2})/);
        return m ? m.slice(1).map(Number) : null;
    };
    const toJalali = value => {
        const p = isoParts(value);
        if (!p) return '';
        const parts = partsFor(new Date(Date.UTC(p[0], p[1] - 1, p[2])));
        return String(parts.year).padStart(4, '0') + '/' + String(parts.month).padStart(2, '0') + '/' + String(parts.day).padStart(2, '0');
    };
    const toIso = value => {
        const raw = digits(value).trim().replace(/[.-]/g, '/');
        if (!raw) return '';
        const m = raw.match(/^(\d{4})\/(\d{1,2})\/(\d{1,2})$/);
        if (!m) throw new Error('تاریخ را شمسی و به شکل سال/ماه/روز وارد کنید؛ مثال: ۱۴۰۵/۰۸/۰۷');
        const target = m.slice(1).map(Number);
        if (target[1] < 1 || target[1] > 12 || target[2] < 1 || target[2] > 31) throw new Error('تاریخ شمسی معتبر نیست.');
        const approx = target[0] + 621;
        for (let t = Date.UTC(approx - 1, 0, 1); t < Date.UTC(approx + 2, 0, 1); t += 86400000) {
            const p = partsFor(new Date(t));
            if (p.year === target[0] && p.month === target[1] && p.day === target[2]) {
                const d = new Date(t);
                return d.getUTCFullYear() + '-' + String(d.getUTCMonth() + 1).padStart(2, '0') + '-' + String(d.getUTCDate()).padStart(2, '0');
            }
        }
        throw new Error('تاریخ شمسی واردشده معتبر نیست.');
    };
    function enhance(input) {
        if (input.dataset.jalaliEnhanced === 'true') return;
        input.dataset.jalaliEnhanced = 'true';
        const nativeValue = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value');
        const display = document.createElement('input');
        display.type = 'text';
        display.inputMode = 'numeric';
        display.autocomplete = 'off';
        display.placeholder = '۱۴۰۵/۰۸/۰۷';
        display.setAttribute('aria-label', 'تاریخ شمسی، قالب سال/ماه/روز');
        display.className = input.className;
        display.style.cssText = input.style.cssText;
        display.required = input.required;
        display.disabled = input.disabled;
        display.readOnly = input.readOnly;
        display.minLength = 8;
        display.maxLength = 10;
        input.required = false;
        input.type = 'hidden';
        input.insertAdjacentElement('afterend', display);
        Object.defineProperty(input, 'value', {
            configurable: true,
            get() { return nativeValue.get.call(this); },
            set(value) {
                nativeValue.set.call(this, value);
                display.value = toJalali(value);
                display.setCustomValidity('');
            }
        });
        display.value = toJalali(nativeValue.get.call(input));
        display.addEventListener('input', () => {
            try {
                const iso = toIso(display.value);
                input.setCustomValidity('');
                display.setCustomValidity('');
                nativeValue.set.call(input, iso);
                input.dispatchEvent(new Event('input', { bubbles: true }));
                input.dispatchEvent(new Event('change', { bubbles: true }));
            } catch (error) {
                nativeValue.set.call(input, '');
                display.setCustomValidity(error.message);
            }
        });
        display.addEventListener('blur', () => {
            if (!display.value.trim()) {
                nativeValue.set.call(input, '');
                display.setCustomValidity('');
            } else if (display.validity.valid) {
                try { display.value = toJalali(toIso(display.value)); } catch {}
            }
        });
        const form = input.form;
        if (form) form.addEventListener('reset', () => setTimeout(() => {
            display.value = toJalali(nativeValue.get.call(input));
            display.setCustomValidity('');
        }, 0));
    }
    function init() {
        document.querySelectorAll('input[type="date"]').forEach(enhance);
        document.documentElement.setAttribute('lang', 'fa');
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init, { once: true });
    else init();
    window.GhoghnoosDates = { toJalali, toIso };
})();