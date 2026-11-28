(() => {
    'use strict';
    const digits = value => String(value ?? '')
        .replace(/[۰-۹]/g, d => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d)))
        .replace(/[٠-٩]/g, d => String('٠١٢٣٤٥٦٧٨٩'.indexOf(d)));
    const jalali = new Intl.DateTimeFormat('fa-IR-u-ca-persian-nu-latn', {
        year: 'numeric', month: '2-digit', day: '2-digit', timeZone: 'UTC'
    });
    const monthNames = new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
        month: 'long', timeZone: 'UTC'
    });
    const partsFor = date => Object.fromEntries(
        jalali.formatToParts(date).filter(x => x.type !== 'literal').map(x => [x.type, Number(x.value)])
    );
    const isoParts = value => {
        const m = String(value || '').match(/^(\d{4})-(\d{2})-(\d{2})/);
        return m ? m.slice(1).map(Number) : null;
    };
    const pad = n => String(n).padStart(2, '0');
    const isoFromDate = d => d.getUTCFullYear() + '-' + pad(d.getUTCMonth() + 1) + '-' + pad(d.getUTCDate());
    const toJalali = value => {
        const p = isoParts(value);
        if (!p) return '';
        const parts = partsFor(new Date(Date.UTC(p[0], p[1] - 1, p[2])));
        return String(parts.year).padStart(4, '0') + '/' + pad(parts.month) + '/' + pad(parts.day);
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
            const d = new Date(t), p = partsFor(d);
            if (p.year === target[0] && p.month === target[1] && p.day === target[2]) return isoFromDate(d);
        }
        throw new Error('تاریخ شمسی واردشده معتبر نیست.');
    };
    const style = document.createElement('style');
    style.textContent = `
        .gh-date-wrap{position:relative;width:100%;min-width:0}
        .gh-date-wrap>input.gh-date-text{width:100%;direction:ltr;text-align:center;cursor:text}
        .gh-date-calendar{position:absolute;z-index:2000;top:calc(100% + 5px);right:0;width:286px;max-width:min(286px,calc(100vw - 32px));padding:10px;background:#fff;color:#24364b;border:1px solid #d6e1ed;border-radius:12px;box-shadow:0 10px 30px #203b5b25;font:13px Tahoma,Arial,sans-serif;direction:rtl}
        .gh-date-calendar[hidden]{display:none}
        .gh-date-cal-head{display:flex;align-items:center;justify-content:space-between;gap:8px;margin-bottom:9px}
        .gh-date-cal-head button{border:1px solid #dce5ef;background:#f5f8fc;border-radius:7px;width:30px;height:30px;cursor:pointer;color:#23496e}
        .gh-date-cal-title{font-weight:700;text-align:center;flex:1}
        .gh-date-grid{display:grid;grid-template-columns:repeat(7,minmax(0,1fr));gap:3px}
        .gh-date-weekday,.gh-date-day{display:flex;align-items:center;justify-content:center;min-height:31px}
        .gh-date-weekday{color:#74869a;font-size:11px}
        .gh-date-day{border:0;border-radius:7px;background:transparent;cursor:pointer;color:inherit}
        .gh-date-day:hover{background:#e8f2ff}
        .gh-date-day.is-today{outline:1px solid #8db8e9}
        .gh-date-day.is-selected{background:#2677c9;color:white}
        .gh-date-day:focus-visible,.gh-date-cal-head button:focus-visible{outline:2px solid #2677c9;outline-offset:1px}
    `;
    document.head.appendChild(style);
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
        display.className = (input.className ? input.className + ' ' : '') + 'gh-date-text';
        display.style.cssText = input.style.cssText;
        display.required = input.required;
        display.disabled = input.disabled;
        display.readOnly = input.readOnly;
        display.maxLength = 10;
        input.required = false;
        input.type = 'hidden';
        input.insertAdjacentElement('afterend', display);
        const wrap = document.createElement('div');
        wrap.className = 'gh-date-wrap';
        display.parentNode.insertBefore(wrap, display);
        wrap.appendChild(display);
        const popup = document.createElement('div');
        popup.className = 'gh-date-calendar';
        popup.hidden = true;
        popup.setAttribute('role', 'dialog');
        popup.setAttribute('aria-label', 'انتخاب تاریخ شمسی');
        wrap.appendChild(popup);
        let viewYear, viewMonth;
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
        const setDate = iso => {
            nativeValue.set.call(input, iso);
            display.value = toJalali(iso);
            display.setCustomValidity('');
            input.setCustomValidity('');
            input.dispatchEvent(new Event('input', { bubbles: true }));
            input.dispatchEvent(new Event('change', { bubbles: true }));
        };
        const close = () => { popup.hidden = true; };
        const monthDates = (year, month) => {
            const dates = [];
            const start = Date.UTC(year + 620, 0, 1);
            const end = Date.UTC(year + 623, 0, 1);
            for (let t = start; t < end; t += 86400000) {
                const d = new Date(t), p = partsFor(d);
                if (p.year === year && p.month === month) dates.push({ iso: isoFromDate(d), day: p.day, weekday: d.getUTCDay() });
            }
            return dates;
        };
        const renderCalendar = () => {
            popup.replaceChildren();
            const head = document.createElement('div');
            head.className = 'gh-date-cal-head';
            const prev = document.createElement('button');
            prev.type = 'button'; prev.textContent = '‹'; prev.setAttribute('aria-label', 'ماه قبل');
            const next = document.createElement('button');
            next.type = 'button'; next.textContent = '›'; next.setAttribute('aria-label', 'ماه بعد');
            const title = document.createElement('div');
            title.className = 'gh-date-cal-title';
            const sample = monthDates(viewYear, viewMonth)[0];
            title.textContent = sample ? monthNames.format(new Date(sample.iso + 'T00:00:00Z')) + ' ' + viewYear : String(viewYear) + '/' + pad(viewMonth);
            prev.addEventListener('click', () => { viewMonth--; if (viewMonth < 1) { viewMonth = 12; viewYear--; } renderCalendar(); });
            next.addEventListener('click', () => { viewMonth++; if (viewMonth > 12) { viewMonth = 1; viewYear++; } renderCalendar(); });
            head.append(prev, title, next); popup.appendChild(head);
            const grid = document.createElement('div'); grid.className = 'gh-date-grid';
            ['ش', 'ی', 'د', 'س', 'چ', 'پ', 'ج'].forEach(label => {
                const el = document.createElement('div'); el.className = 'gh-date-weekday'; el.textContent = label; grid.appendChild(el);
            });
            const dates = monthDates(viewYear, viewMonth);
            if (dates.length) {
                const offset = (dates[0].weekday + 1) % 7;
                for (let i = 0; i < offset; i++) grid.appendChild(document.createElement('div'));
                const todayIso = isoFromDate(new Date(Date.UTC(new Date().getFullYear(), new Date().getMonth(), new Date().getDate())));
                const selectedIso = (() => { try { return toIso(display.value); } catch { return ''; } })();
                dates.forEach(item => {
                    const button = document.createElement('button');
                    button.type = 'button'; button.className = 'gh-date-day'; button.textContent = item.day;
                    button.setAttribute('aria-label', toJalali(item.iso));
                    if (item.iso === todayIso) button.classList.add('is-today');
                    if (item.iso === selectedIso) button.classList.add('is-selected');
                    button.addEventListener('click', () => { setDate(item.iso); close(); display.focus(); });
                    grid.appendChild(button);
                });
            }
            popup.appendChild(grid);
        };
        const openCalendar = () => {
            if (display.disabled || display.readOnly) return;
            let p;
            try { p = display.value ? (() => { const iso = toIso(display.value); return partsFor(new Date(iso + 'T00:00:00Z')); })() : null; } catch { p = null; }
            if (!p) {
                const now = new Date();
                p = partsFor(new Date(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate())));
            }
            viewYear = p.year; viewMonth = p.month;
            renderCalendar(); popup.hidden = false;
        };
        display.addEventListener('click', openCalendar);
        display.addEventListener('keydown', event => {
            if (event.key === 'Escape') close();
            if (event.key === 'ArrowDown' && (event.altKey || event.ctrlKey)) { event.preventDefault(); openCalendar(); }
        });
        display.addEventListener('input', () => {
            const value = display.value.trim();
            if (!value) {
                nativeValue.set.call(input, '');
                display.setCustomValidity('');
                input.setCustomValidity('');
                return;
            }
            try {
                const iso = toIso(value);
                nativeValue.set.call(input, iso);
                display.setCustomValidity('');
                input.setCustomValidity('');
                input.dispatchEvent(new Event('input', { bubbles: true }));
                input.dispatchEvent(new Event('change', { bubbles: true }));
            } catch {
                nativeValue.set.call(input, '');
                display.setCustomValidity('');
            }
        });
        display.addEventListener('blur', () => {
            if (!display.value.trim()) {
                nativeValue.set.call(input, '');
                display.setCustomValidity('');
            } else {
                try {
                    const iso = toIso(display.value);
                    display.value = toJalali(iso);
                    setDate(iso);
                } catch {
                    display.setCustomValidity('تاریخ شمسی معتبر نیست؛ نمونه: ۱۴۰۵/۰۸/۰۷');
                }
            }
        });
        document.addEventListener('pointerdown', event => { if (!wrap.contains(event.target)) close(); });
        const form = input.form;
        if (form) form.addEventListener('reset', () => setTimeout(() => {
            display.value = toJalali(nativeValue.get.call(input));
            display.setCustomValidity('');
            close();
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