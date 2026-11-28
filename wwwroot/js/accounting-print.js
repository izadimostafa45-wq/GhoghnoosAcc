(() => {
  'use strict';

  const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g, ch => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
  })[ch]);

  const text = value => value === null || value === undefined || value === '' ? '—' : String(value);

  function printDocument(options) {
    if (!options || typeof options !== 'object') throw new Error('اطلاعات سند برای چاپ معتبر نیست.');

    const title = text(options.title || 'سند حسابداری');
    const theme = options.theme === 'invoice-premium' ? ' invoice-premium' : '';
    const subtitle = text(options.subtitle || 'سامانه حسابداری و مدیریت مالی ققنوس');
    const number = text(options.number);
    const metadata = Array.isArray(options.metadata) ? options.metadata : [];
    const headers = Array.isArray(options.table?.headers) ? options.table.headers : [];
    const rows = Array.isArray(options.table?.rows) ? options.table.rows : [];
    const totals = Array.isArray(options.totals) ? options.totals : [];
    const notes = text(options.notes);
    const signatures = Array.isArray(options.signatures) && options.signatures.length
      ? options.signatures : ['تهیه‌کننده / صادرکننده', 'بررسی و تأیید'];

    const metadataHtml = metadata.map(item =>
      '<div class="meta-item"><span class="label">' + escapeHtml(item.label) +
      '</span><strong>' + escapeHtml(text(item.value)) + '</strong></div>'
    ).join('');

    const tableHtml = headers.length
      ? '<table class="items"><thead><tr>' + headers.map(h => '<th>' + escapeHtml(h) + '</th>').join('') +
        '</tr></thead><tbody>' + (rows.length
          ? rows.map(row => '<tr>' + headers.map((_, i) => '<td>' + escapeHtml(Array.isArray(row) ? text(row[i]) : '') + '</td>').join('') + '</tr>').join('')
          : '<tr><td colspan="' + headers.length + '" class="empty">ردیفی برای نمایش ثبت نشده است.</td></tr>') +
        '</tbody></table>'
      : '';

    const totalsHtml = totals.length
      ? '<section class="totals">' + totals.map(item =>
        '<div class="total-row' + (item.emphasis ? ' emphasis' : '') + '"><span>' + escapeHtml(item.label) +
        '</span><strong>' + escapeHtml(text(item.value)) + '</strong></div>'
      ).join('') + '</section>'
      : '';

    const notesHtml = notes !== '—' ? '<section class="notes"><strong>توضیحات / شرح</strong><p>' + escapeHtml(notes) + '</p></section>' : '';
    const signaturesHtml = '<section class="signatures">' + signatures.map(label =>
      '<div>' + escapeHtml(label) + '</div>'
    ).join('') + '</section>';

    const html = '<!doctype html><html lang="fa" dir="rtl"><head><meta charset="utf-8">' +
      '<meta name="viewport" content="width=device-width,initial-scale=1"><title>' + escapeHtml(title + ' ' + number) +
      '</title><style>' +
      '*{box-sizing:border-box}html{background:#fff}body{margin:0;background:#fff;color:#000;font:12px/1.9 Tahoma,Arial,sans-serif}' +
      '.sheet{max-width:1000px;margin:20px auto;padding:28px;background:#fff;border:1px solid #777}' +
      '.top{display:flex;justify-content:space-between;align-items:flex-start;gap:20px;padding-bottom:15px;border-bottom:2px solid #000}' +
      '.brand h1{font-size:20px;margin:0;color:#000}.brand p{font-size:10px;margin:4px 0 0;color:#333}' +
      '.document-title{text-align:left}.document-title h2{font-size:21px;margin:0 0 4px;color:#000}.document-title span{font-size:10px;color:#333}' +
      '.meta{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:9px;margin:18px 0}' +
      '.meta-item{min-width:0;padding:10px 12px;border:1px solid #888;background:#fff}.label{display:block;font-size:10px;color:#444;margin-bottom:4px}.meta-item strong{font-size:12px;overflow-wrap:anywhere;color:#000}' +
      '.items{width:100%;border-collapse:collapse;margin-top:16px}.items th,.items td{padding:8px;border:1px solid #777;text-align:right;vertical-align:top;overflow-wrap:anywhere}' +
      '.items th{font-weight:bold;background:#fff;color:#000;border-bottom:2px solid #000}.items tr{break-inside:avoid}.items .empty{text-align:center;color:#333}' +
      '.num{white-space:nowrap;font-variant-numeric:tabular-nums}.totals{width:min(100%,440px);margin:16px 0 0 auto;border:1px solid #777}' +
      '.total-row{display:flex;justify-content:space-between;gap:18px;padding:8px 11px;border-bottom:1px solid #bbb}.total-row:last-child{border-bottom:0}.total-row.emphasis{font-size:14px;font-weight:bold;border-top:2px solid #000}' +
      '.notes{margin-top:16px;padding:10px 12px;border:1px dashed #777}.notes strong{font-size:11px}.notes p{margin:5px 0 0;white-space:pre-wrap;overflow-wrap:anywhere}' +
      '.signatures{display:flex;justify-content:space-between;gap:35px;margin-top:38px;text-align:center}.signatures div{width:42%;min-height:45px;padding-top:9px;border-top:1px dashed #555;color:#222;font-size:11px}' +
      '.footer{display:flex;justify-content:space-between;gap:15px;margin-top:24px;padding-top:9px;border-top:1px solid #888;font-size:9px;color:#333}' +
      '.sheet.invoice-premium{max-width:1000px;padding:30px 32px;border:1px solid #222;border-radius:16px;position:relative}' +
      '.invoice-premium .top{align-items:center;padding:0 0 20px;border-bottom:3px double #111;position:relative}' +
      '.invoice-premium .brand h1{font-size:27px;letter-spacing:1px;font-weight:800}' +
      '.invoice-premium .brand p{font-size:10px;letter-spacing:.2px;margin-top:6px}' +
      '.invoice-premium .document-title{text-align:left;min-width:185px;padding:13px 16px;border:1px solid #222;border-radius:10px;position:relative}' +
      '.invoice-premium .document-title:before{content:"";position:absolute;top:-1px;right:-1px;left:-1px;height:4px;background:#111}' +
      '.invoice-premium .document-title h2{font-size:22px;line-height:1.5;margin:0 0 5px}' +
      '.invoice-premium .document-title span{display:block;font-size:12px;font-weight:700;padding-top:4px;border-top:1px solid #aaa}' +
      '.invoice-premium .meta{grid-template-columns:repeat(2,minmax(0,1fr));gap:0;margin:20px 0 22px;border:1px solid #777;border-radius:11px;overflow:hidden}' +
      '.invoice-premium .meta-item{display:flex;align-items:center;gap:12px;padding:9px 12px;border:0;border-bottom:1px solid #ccc;background:#fff}' +
      '.invoice-premium .meta-item:nth-child(odd){border-left:1px solid #ccc}' +
      '.invoice-premium .meta-item:nth-last-child(-n+2){border-bottom:0}' +
      '.invoice-premium .meta-item .label{min-width:75px;margin:0;font-size:10px}' +
      '.invoice-premium .meta-item strong{font-size:12px;font-weight:700}' +
      '.invoice-premium .items{margin-top:22px;border:1px solid #888;border-radius:10px;border-collapse:separate;border-spacing:0;overflow:hidden}' +
      '.invoice-premium .items th{font-size:10px;padding:10px 7px;border-top:1px solid #111;border-bottom:2px solid #111;background:#f2f2f2}' +
      '.invoice-premium .items td{padding:9px 7px;border-color:#aaa}' +
      '.invoice-premium .items tbody tr:nth-child(even) td{background:#f8f8f8}' +
      '.invoice-premium .items td:last-child{font-weight:700}' +
      '.invoice-premium .totals{width:min(100%,470px);margin-top:20px;border:1px solid #333;border-radius:11px;overflow:hidden}' +
      '.invoice-premium .total-row{padding:8px 13px;border-bottom:1px solid #ddd}' +
      '.invoice-premium .total-row.emphasis{margin-top:0;padding:13px;font-size:15px;background:#ededed;border-top:2px solid #111}' +
      '.invoice-premium .notes{margin-top:22px;border:1px solid #aaa;border-radius:10px}' +
      '.invoice-premium .signatures{margin-top:48px;gap:45px}' +
      '.invoice-premium .signatures div{min-height:55px;border-top:1px solid #444;border-radius:8px 8px 0 0;padding-top:10px}' +
      '.invoice-premium .footer{margin-top:30px;padding-top:10px;border-top:1px solid #222;font-size:9px}' +
      '.no-print{text-align:center;margin-top:20px}.no-print button{border:1px solid #000;background:#fff;color:#000;padding:9px 18px;font:inherit;cursor:pointer}' +
      '@page{size:A4;margin:12mm}@media print{html,body{width:100%!important;max-width:100%!important;margin:0!important;padding:0!important;background:#fff!important;color:#000!important}.sheet{max-width:none;margin:0;padding:0;border:0}.sheet.invoice-premium{width:100%!important;max-width:100%!important;margin:0 auto!important;padding:0!important;border:0!important;border-radius:0!important;overflow:visible!important}.no-print{display:none!important}*{background:#fff!important;background-image:none!important;color:#000!important;box-shadow:none!important;text-shadow:none!important;border-color:#666!important;-webkit-print-color-adjust:economy!important;print-color-adjust:economy!important}.top{border-bottom:2px solid #000!important}.items th{border-bottom:2px solid #000!important}thead{display:table-header-group}tr{break-inside:avoid}.meta-item,.notes,.totals{break-inside:avoid}}' +
      '</style></head><body><main class="sheet' + theme + '"><header class="top"><div class="brand"><h1>ققنوس</h1><p>' +
      escapeHtml(subtitle) + '</p></div><div class="document-title"><h2>' + escapeHtml(title) +
      '</h2><span>شماره سند: ' + escapeHtml(number) + '</span></div></header>' +
      (metadataHtml ? '<section class="meta">' + metadataHtml + '</section>' : '') +
      tableHtml + totalsHtml + notesHtml + signaturesHtml +
      '<footer class="footer"><span>تهیه‌شده توسط سامانه حسابداری ققنوس</span><span>شماره: ' + escapeHtml(number) +
      '</span></footer><div class="no-print"><button type="button" onclick="window.print()">چاپ / ذخیره PDF</button></div></main></body></html>';

    const win = window.open('', '_blank', 'width=1000,height=850');
    if (!win) {
      alert('برای چاپ سند، اجازه بازشدن پنجره جدید (Pop-up) را در مرورگر فعال کنید.');
      return false;
    }
    win.document.open();
    win.document.write(html);
    win.document.close();
    win.focus();
    win.setTimeout(() => { if (!win.closed) win.print(); }, 350);
    return true;
  }

  window.GhoghnoosPrint = Object.freeze({ printDocument });
})();
