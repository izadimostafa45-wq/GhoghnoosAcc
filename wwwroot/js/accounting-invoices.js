
(() => {
 // Keep invoice-page JavaScript inside this script block; shared by both Sales and Purchase routes.
 const $=id=>document.getElementById(id), money=n=>Number(n||0).toLocaleString('fa-IR',{maximumFractionDigits:2}); let accounts=[],editingInvoiceId=null;
 const requestedKind=new URLSearchParams(window.location.search).get('kind');
 if(requestedKind==='Sales'||requestedKind==='Purchase') { $('kind').value=requestedKind; $('kind').disabled=true; }
 $('kind').addEventListener('change',()=>{ $('kind').disabled=false; });
 const today=new Date(); $('invoiceDate').value=new Date(today.getTime()-today.getTimezoneOffset()*60000).toISOString().slice(0,10);
 function addLine(line={description:'',quantity:1,unitPrice:0,discount:0,taxRate:0}) {
   const tr=document.createElement('tr');
   const fields=[['description','text',line.description,200],['quantity','number',line.quantity,''],['unitPrice','number',line.unitPrice,''],['discount','number',line.discount||0,''],['taxRate','number',line.taxRate,'']];
   for(const [key,type,value,max] of fields){const td=document.createElement('td'),input=document.createElement('input');input.dataset.field=key;input.type=type;input.value=value;input.required=key==='description';if(type==='number'){input.min='0';input.step=key==='quantity'?'0.001':'0.01';}if(max)input.maxLength=max;input.addEventListener('input',calculate);td.append(input);tr.append(td);}
   const amount=document.createElement('td');amount.className='line-total';amount.textContent='۰';tr.append(amount);
   const action=document.createElement('td'),remove=document.createElement('button');remove.type='button';remove.className='secondary';remove.textContent='حذف';remove.addEventListener('click',()=>{tr.remove();calculate();});action.append(remove);tr.append(action);$('lines').append(tr);calculate();
 }
 function calculate(){let subtotal=0,tax=0;for(const tr of $('lines').rows){const get=k=>tr.querySelector('[data-field="'+k+'"]').value;const gross=Math.max(0,Number(get('quantity'))||0)*Math.max(0,Number(get('unitPrice'))||0);const discount=Math.min(gross,Math.max(0,Number(get('discount'))||0));const base=gross-discount;const t=base*Math.max(0,Number(get('taxRate'))||0)/100;subtotal+=base;tax+=t;tr.querySelector('.line-total').textContent=money(base+t);} $('subtotal').textContent=money(subtotal)+' ریال';$('tax').textContent=money(tax)+' ریال';$('total').textContent=money(subtotal+tax)+' ریال';}
 async function loadParties(){const r=await fetch('/api/accounting/parties'),data=await r.json();if(!r.ok)throw Error(data.message||'بارگذاری اشخاص ناموفق بود');$('party').replaceChildren(new Option('انتخاب طرف حساب',''));for(const p of data.filter(x=>x.isActive))$('party').add(new Option(p.name,p.id));}
 async function loadAccounts(){const r=await fetch('/api/accounting/accounts'),data=await r.json();if(!r.ok)throw Error(data.message||'بارگذاری حساب‌ها ناموفق بود');accounts=data.filter(x=>x.isActive);}
 function accountSelect(type,placeholder){const select=document.createElement('select');select.required=true;select.append(new Option(placeholder,''));for(const a of accounts.filter(x=>x.type===type).sort((a,b)=>a.code.localeCompare(b.code)))select.append(new Option(a.code+' - '+a.title,a.id));select.setAttribute('aria-label',placeholder);select.style.maxWidth='230px';select.style.margin='3px';return select;}
 async function loadInvoices(){const r=await fetch('/api/accounting/invoices'),data=await r.json();if(!r.ok)throw Error(data.message||'بارگذاری فاکتورها ناموفق بود');const selectedKind=$('kind').value;const filteredData=data.filter(x=>x.kind===selectedKind);const body=$('invoiceRows');body.replaceChildren();for(const x of filteredData){const tr=document.createElement('tr');for(const v of [x.number,x.kind==='Sales'?'فروش':'خرید',new Date(x.invoiceDate).toLocaleDateString('fa-IR'),x.partyName,money(x.total)+' ریال',money(x.amountPaid)+' ریال',money(x.remaining)+' ریال',x.status==='Posted'?'ثبت نهایی':'پیش‌نویس']){const td=document.createElement('td');td.textContent=v;tr.append(td);}const td=document.createElement('td');const menu=document.createElement('div');menu.className='invoice-menu';const menuTrigger=document.createElement('button');menuTrigger.type='button';menuTrigger.className='invoice-menu-trigger';menuTrigger.textContent='⋮';menuTrigger.title='عملیات فاکتور';menuTrigger.setAttribute('aria-label','عملیات فاکتور '+x.number);menuTrigger.setAttribute('aria-haspopup','true');const menuPop=document.createElement('div');menuPop.className='invoice-menu-pop';const printAction=document.createElement('button');printAction.type='button';printAction.innerHTML='<span class="menu-icon">▤</span><span>چاپ فاکتور مشتری</span>';printAction.addEventListener('click',()=>printInvoice(x.id,x));menuPop.append(printAction);menu.append(menuTrigger,menuPop);td.append(menu);if(x.status==='Draft'){const edit=document.createElement('button');edit.className='secondary';edit.textContent='ویرایش';edit.addEventListener('click',()=>editInvoice(x.id));const del=document.createElement('button');del.className='secondary';del.textContent='حذف پیش‌نویس';del.addEventListener('click',()=>deleteInvoice(x.id,x.number));td.append(edit,del);const counterparty=accountSelect(x.kind==='Sales'?'Asset':'Liability',x.kind==='Sales'?'حساب دریافتنی':'حساب پرداختنی');const operating=accountSelect(x.kind==='Sales'?'Revenue':'Expense',x.kind==='Sales'?'حساب درآمد':'حساب هزینه');const tax=accountSelect(x.kind==='Sales'?'Liability':'Asset','حساب مالیات (در صورت نیاز)');tax.required=Number(x.taxTotal)>0;if(Number(x.taxTotal)===0)tax.append(new Option('بدون مالیات',''));const b=document.createElement('button');b.textContent='ثبت نهایی و صدور سند';b.addEventListener('click',()=>postInvoice(x.id,x.kind,counterparty,operating,tax));td.append(counterparty,operating);if(Number(x.taxTotal)>0)td.append(tax);td.append(b);}tr.append(td);body.append(tr);}if(!filteredData.length){const tr=document.createElement('tr'),td=document.createElement('td');td.colSpan=9;td.textContent=data.length?'برای این نوع فاکتور موردی ثبت نشده است.':'هنوز فاکتوری ثبت نشده است.';tr.append(td);body.append(tr);}}
 async function printInvoice(id, summary) {
  try {
    const response = await fetch('/api/accounting/invoices/' + id);
    const data = await response.json();
    if (!response.ok) throw Error(data.message || 'دریافت جزئیات فاکتور برای چاپ ناموفق بود');
    if (!window.GhoghnoosPrint || typeof window.GhoghnoosPrint.printDocument !== 'function') {
      throw Error('مرکز چاپ بارگذاری نشده است. صفحه را بازخوانی کنید.');
    }

    const kind = data.kind || summary.kind;
    const lines = Array.isArray(data.lines) ? data.lines : [];
    let subtotal = 0, taxTotal = 0, discountTotal = 0;
    const rows = lines.map((line, index) => {
      const quantity = Number(line.quantity) || 0;
      const unitPrice = Number(line.unitPrice) || 0;
      const gross = quantity * unitPrice;
      const discount = Math.min(gross, Math.max(0, Number(line.discount) || 0));
      const taxable = gross - discount;
      const tax = taxable * Math.max(0, Number(line.taxRate) || 0) / 100;
      const lineTotal = taxable + tax;
      subtotal += taxable;
      taxTotal += tax;
      discountTotal += discount;
      return [
        (index + 1).toLocaleString('fa-IR'),
        line.description || '—',
        quantity.toLocaleString('fa-IR'),
        unitPrice.toLocaleString('fa-IR'),
        discount.toLocaleString('fa-IR'),
        (Number(line.taxRate) || 0).toLocaleString('fa-IR') + '٪',
        lineTotal.toLocaleString('fa-IR', { maximumFractionDigits: 2 })
      ];
    });

    const total = Number(data.total ?? summary.total ?? subtotal + taxTotal) || 0;
    const invoiceDate = new Date(data.invoiceDate || summary.invoiceDate);
    const dateText = Number.isNaN(invoiceDate.getTime()) ? '—' : invoiceDate.toLocaleDateString('fa-IR');
    const paid = Number(data.amountPaid ?? summary.amountPaid) || 0;
    const remaining = Number(data.remaining ?? summary.remaining ?? (total - paid)) || 0;
    const party = data.partyName || summary.partyName || $('party').selectedOptions[0]?.textContent || '—';
    const isPurchase = kind === 'Purchase';
    const title = isPurchase ? 'فاکتور خرید' : 'فاکتور فروش';

    window.GhoghnoosPrint.printDocument({
      title,
      subtitle: 'سامانه حسابداری و مدیریت مالی',
      number: data.number || summary.number || '—',
      metadata: [
        { label: 'شماره فاکتور', value: data.number || summary.number || '—' },
        { label: 'تاریخ صدور', value: dateText },
        { label: 'وضعیت', value: (data.status || summary.status) === 'Posted' ? 'ثبت نهایی' : 'پیش‌نویس' },
        { label: isPurchase ? 'تأمین‌کننده' : 'مشتری', value: party },
        { label: 'نوع فاکتور', value: title },
        { label: 'واحد مبلغ', value: 'ریال' }
      ],
      table: {
        headers: ['ردیف', 'شرح کالا یا خدمت', 'تعداد', 'قیمت واحد (ریال)', 'تخفیف (ریال)', 'مالیات', 'مبلغ نهایی (ریال)'],
        rows
      },
      totals: [
        { label: 'جمع پس از تخفیف (ریال)', value: subtotal.toLocaleString('fa-IR', { maximumFractionDigits: 2 }) },
        { label: 'مجموع تخفیف (ریال)', value: discountTotal.toLocaleString('fa-IR', { maximumFractionDigits: 2 }) },
        { label: 'مالیات و عوارض (ریال)', value: taxTotal.toLocaleString('fa-IR', { maximumFractionDigits: 2 }) },
        { label: 'مبلغ نهایی (ریال)', value: total.toLocaleString('fa-IR', { maximumFractionDigits: 2 }), emphasis: true },
        { label: 'پرداخت‌شده (ریال)', value: paid.toLocaleString('fa-IR', { maximumFractionDigits: 2 }) },
        { label: 'مانده حساب (ریال)', value: remaining.toLocaleString('fa-IR', { maximumFractionDigits: 2 }) }
      ],
      notes: data.notes || '',
      signatures: ['مهر و امضای صادرکننده', 'امضا / تأیید مشتری']
    });
  } catch (error) {
    $('message').textContent = error.message || 'آماده‌سازی چاپ فاکتور ناموفق بود.';
  }
}
function resetForm(){editingInvoiceId=null;$('kind').disabled=false;$('number').value='';$('notes').value='';$('lines').replaceChildren();addLine();$('saveDraft').textContent='ذخیره پیش‌نویس';const cancel=$('cancelEdit');if(cancel)cancel.remove();}
 async function editInvoice(id){try{const r=await fetch('/api/accounting/invoices/'+id),d=await r.json();if(!r.ok)throw Error(d.message||'دریافت فاکتور ناموفق بود');editingInvoiceId=id;$('kind').value=d.kind;$('kind').disabled=true;$('number').value=d.number;$('invoiceDate').value=new Date(d.invoiceDate).toISOString().slice(0,10);$('party').value=String(d.partyId);$('notes').value=d.notes||'';$('lines').replaceChildren();for(const line of d.lines)addLine(line);$('saveDraft').textContent='ذخیره تغییرات';if(!$('cancelEdit')){const b=document.createElement('button');b.id='cancelEdit';b.type='button';b.className='secondary';b.textContent='لغو ویرایش';b.addEventListener('click',resetForm);$('saveDraft').after(b);}window.scrollTo({top:0,behavior:'smooth'});$('message').textContent='در حال ویرایش پیش‌نویس شماره '+d.number;}catch(e){$('message').textContent=e.message;}}
 async function deleteInvoice(id,number){if(!confirm('پیش‌نویس فاکتور '+number+' حذف شود؟ این کار قابل بازگشت نیست.'))return;try{const r=await fetch('/api/accounting/invoices/'+id,{method:'DELETE'}),d=await r.json();if(!r.ok)throw Error(d.message||'حذف فاکتور ناموفق بود');$('message').textContent=d.message||'پیش‌نویس حذف شد.';if(editingInvoiceId===id)resetForm();await loadInvoices();}catch(e){$('message').textContent=e.message;}}
 async function postInvoice(id,kind,counterparty,operating,tax){if(!counterparty.value||!operating.value||(tax.required&&!tax.value)){ $('message').textContent='حساب دریافتنی/پرداختنی، درآمد/هزینه و در صورت وجود مالیات را انتخاب کنید.';return;}if(!confirm('فاکتور ثبت نهایی شود و سند حسابداری مرتبط ایجاد شود؟'))return;try{const payload={counterpartyAccountId:Number(counterparty.value),operatingAccountId:Number(operating.value),taxAccountId:tax.value?Number(tax.value):null};const r=await fetch('/api/accounting/invoices/'+id+'/post',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)}),d=await r.json();if(!r.ok)throw Error(d.message||'ثبت نهایی ناموفق بود');$('message').textContent=d.message||'فاکتور و سند حسابداری ثبت شدند.';await loadInvoices();const settle=confirm((kind==='Purchase'?'فاکتور خرید':'فاکتور فروش')+' با موفقیت ثبت نهایی شد. آیا مایل به '+(kind==='Purchase'?'پرداخت وجه این فاکتور':'دریافت وجه این فاکتور')+' هستید؟');if(settle){const paymentKind=kind==='Purchase'?'Payment':'Receipt';window.location.assign('/Accounting/Payments?kind='+paymentKind+'&invoiceId='+encodeURIComponent(id));}}catch(e){$('message').textContent=e.message;}}
 $('addLine').addEventListener('click',()=>addLine());$('kind').addEventListener('change',()=>loadInvoices().catch(e=>$('message').textContent=e.message));$('refresh').addEventListener('click',()=>loadInvoices().catch(e=>$('message').textContent=e.message));
 $('saveDraft').addEventListener('click',async()=>{try{const lines=[...$('lines').rows].map(tr=>Object.fromEntries([...tr.querySelectorAll('[data-field]')].map(i=>[i.dataset.field,i.type==='number'?Number(i.value):i.value.trim()])));if(!lines.length||lines.some(x=>!x.description||x.quantity<=0||x.unitPrice<0||x.discount<0||x.taxRate<0))throw Error('حداقل یک ردیف معتبر وارد کنید؛ تعداد باید مثبت باشد.');const payload={kind:$('kind').value,number:$('number').value.trim(),invoiceDate:$('invoiceDate').value,partyId:Number($('party').value),notes:$('notes').value.trim(),lines};const url=editingInvoiceId?'/api/accounting/invoices/'+editingInvoiceId:'/api/accounting/invoices';const r=await fetch(url,{method:editingInvoiceId?'PUT':'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)}),d=await r.json();if(!r.ok)throw Error(d.message||'ذخیره فاکتور ناموفق بود');$('message').textContent=d.message||(editingInvoiceId?'پیش‌نویس فاکتور ویرایش شد.':'پیش‌نویس فاکتور ذخیره شد.');resetForm();await loadInvoices();}catch(e){$('message').textContent=e.message;}});
 addLine();Promise.all([loadParties(),loadAccounts()]).then(loadInvoices).catch(e=>$('message').textContent=e.message);
})();
