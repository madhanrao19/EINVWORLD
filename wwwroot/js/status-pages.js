/* Error pages (Ledger mockup "Error pages"). Pages/Error.cshtml renders <div class="ew" id="ew" data-code="404">
   and this script fills it in, in English or Bahasa Malaysia. Copied from the EinvWorld Ledger mockup. */
var EW_CONFIG = {
  home: "/",
  login: "/Identity/Account/Login",
  invoices: "/Invoices/InvoiceLists",
  email: "info@einvworld.com",
  phone: "",
  maxUpload: "32 MB",  // Program.cs MaxRequestBytes
  tz: "Asia/Kuala_Lumpur"
};
var EW = (function(){
  var D = {"common": {"en": {"eyebrow": "Error {c}", "eyebrowOff": "No connection", "whatNow": "What you can do", "back": "Go back", "reload": "Try again", "home": "Go to dashboard", "login": "Sign in again", "switch": "Use another account", "invoices": "Open Invoice Management", "tech": "Technical details", "code": "Error code", "time": "Time", "page": "Page", "ref": "Reference", "copy": "Copy details", "copied": "Copied", "help": "Still stuck? Contact support at {email} and quote reference {ref}.", "urgent": "Need to issue an e-invoice urgently? You can still use the {myinvois}.", "checking": "Checking…", "next": "Not back yet. Next check in {s}s", "up": "We're back. Reloading…", "offline": "Waiting for your connection to return…", "retryIn": "Retrying automatically in {s}s", "lang": "Language"}, "ms": {"eyebrow": "Ralat {c}", "eyebrowOff": "Tiada sambungan", "whatNow": "Apa yang boleh anda lakukan", "back": "Kembali", "reload": "Cuba lagi", "home": "Ke papan pemuka", "login": "Log masuk semula", "switch": "Guna akaun lain", "invoices": "Buka Pengurusan Invois", "tech": "Butiran teknikal", "code": "Kod ralat", "time": "Masa", "page": "Halaman", "ref": "Rujukan", "copy": "Salin butiran", "copied": "Disalin", "help": "Masih perlukan bantuan? Hubungi sokongan di {email} dan nyatakan rujukan {ref}.", "urgent": "Perlu mengeluarkan e-invois dengan segera? Anda masih boleh menggunakan {myinvois}.", "checking": "Sedang menyemak…", "next": "Belum pulih. Semakan seterusnya dalam {s}s", "up": "Sistem telah kembali. Memuat semula…", "offline": "Menunggu sambungan anda pulih…", "retryIn": "Mencuba semula secara automatik dalam {s}s", "lang": "Bahasa"}}, "pages": {"400": {"icon": "alert", "tone": "info", "actions": ["back", "home"], "en": {"title": "We couldn't process that request", "lead": "Part of the request was incomplete or out of date. This usually happens when a form is left open too long, or a link is broken.", "tips": ["Go back, refresh the page, then try again.", "If you were submitting a form, check whether it was already saved before you submit it again."]}, "ms": {"title": "Permintaan tidak dapat diproses", "lead": "Sebahagian permintaan tidak lengkap atau sudah lapuk. Ini biasanya berlaku apabila borang dibiarkan terbuka terlalu lama, atau pautan rosak.", "tips": ["Kembali, muat semula halaman, kemudian cuba lagi.", "Jika anda menghantar borang, semak sama ada ia telah disimpan sebelum menghantarnya semula."]}}, "401": {"icon": "lock", "tone": "neutral", "actions": ["login"], "en": {"title": "Your session has ended", "lead": "For your security, you were signed out after a period of inactivity, or because you signed in on another device.", "tips": ["Sign in again and you'll come straight back to this page.", "Changes you hadn't saved may need to be entered again."]}, "ms": {"title": "Sesi anda telah tamat", "lead": "Demi keselamatan, anda telah dilog keluar selepas tiada aktiviti untuk satu tempoh, atau kerana anda log masuk di peranti lain.", "tips": ["Log masuk semula dan anda akan kembali terus ke halaman ini.", "Perubahan yang belum disimpan mungkin perlu dimasukkan semula."]}}, "403": {"icon": "shield", "tone": "neutral", "actions": ["home", "switch"], "en": {"title": "You don't have access to this page", "lead": "Your account role doesn't include this page, or the record belongs to another company (TIN).", "tips": ["Check that you're signed in with the right account.", "Ask your company administrator to give you access."]}, "ms": {"title": "Anda tiada akses ke halaman ini", "lead": "Peranan akaun anda tidak merangkumi halaman ini, atau rekod ini milik syarikat (TIN) lain.", "tips": ["Pastikan anda log masuk dengan akaun yang betul.", "Minta pentadbir syarikat anda memberikan akses."]}}, "404": {"icon": "search", "tone": "info", "actions": ["home", "invoices"], "en": {"title": "We can't find that page", "lead": "The link may be mistyped, or the page may have moved.", "tips": ["Check the address in your browser bar.", "Looking for an invoice? Search by invoice number, buyer or TIN in Invoice Management."]}, "ms": {"title": "Halaman tidak ditemui", "lead": "Pautan mungkin salah taip, atau halaman telah dialihkan.", "tips": ["Semak alamat dalam bar pelayar anda.", "Mencari invois? Cari mengikut nombor invois, pembeli atau TIN dalam Pengurusan Invois."]}}, "413": {"icon": "upload", "tone": "info", "actions": ["back"], "en": {"title": "That file is too large", "lead": "The upload is over the {max} limit.", "tips": ["Compress the PDF, or scan at a lower resolution. 200 dpi is enough.", "Split a document that holds several invoices into separate files."]}, "ms": {"title": "Fail terlalu besar", "lead": "Fail yang dimuat naik melebihi had {max}.", "tips": ["Mampatkan PDF, atau imbas pada resolusi lebih rendah. 200 dpi sudah memadai.", "Pisahkan dokumen yang mengandungi beberapa invois kepada fail berasingan."]}}, "429": {"icon": "clock", "tone": "warn", "actions": ["reload"], "auto": "countdown", "en": {"title": "Too many requests", "lead": "You've made a lot of requests in a short time, so we've paused them briefly to keep EinvWorld fast for everyone.", "tips": ["Wait a moment. This page tries again by itself.", "Avoid clicking Submit repeatedly. One click is enough."]}, "ms": {"title": "Terlalu banyak permintaan", "lead": "Anda telah membuat banyak permintaan dalam masa singkat, jadi kami menghentikannya seketika supaya EinvWorld kekal pantas untuk semua.", "tips": ["Tunggu sebentar. Halaman ini akan mencuba semula dengan sendirinya.", "Elakkan menekan Hantar berulang kali. Sekali tekan sudah memadai."]}}, "500": {"icon": "alert", "tone": "warn", "actions": ["reload", "home"], "en": {"title": "Something went wrong on our side", "lead": "The error has been logged. It wasn't caused by anything you did.", "tips": ["Try again in a minute.", "If you were submitting an invoice, check Invoice Management before you resubmit. It may already have gone through."]}, "ms": {"title": "Berlaku ralat pada pihak kami", "lead": "Ralat ini telah direkodkan. Ia bukan berpunca daripada tindakan anda.", "tips": ["Cuba lagi dalam seminit.", "Jika anda sedang menghantar invois, semak Pengurusan Invois sebelum menghantar semula. Ia mungkin sudah berjaya."]}}, "502": {"icon": "unlink", "tone": "warn", "actions": ["reload"], "auto": "poll", "en": {"title": "EinvWorld is temporarily unreachable", "lead": "Our server didn't respond properly. This is usually brief, and this page reloads by itself as soon as we're back.", "tips": ["@urgent"]}, "ms": {"title": "EinvWorld tidak dapat dicapai buat sementara", "lead": "Pelayan kami tidak memberi respons dengan betul. Ini biasanya sekejap sahaja, dan halaman ini akan dimuat semula sebaik sahaja sistem kembali.", "tips": ["@urgent"]}}, "503": {"icon": "cloud", "tone": "warn", "actions": ["reload"], "auto": "poll", "en": {"title": "Service temporarily unavailable", "lead": "EinvWorld is overloaded or restarting. We're on it, and this page reloads by itself when the service is back.", "tips": ["Your saved data is safe. Invoices already submitted to LHDN are not affected.", "@urgent"]}, "ms": {"title": "Perkhidmatan tidak tersedia buat sementara", "lead": "EinvWorld sedang terlalu sibuk atau dimulakan semula. Kami sedang menanganinya, dan halaman ini akan dimuat semula apabila perkhidmatan kembali.", "tips": ["Data yang telah disimpan adalah selamat. Invois yang telah dihantar kepada LHDN tidak terjejas.", "@urgent"]}}, "504": {"icon": "hourglass", "tone": "warn", "actions": ["reload", "invoices"], "en": {"title": "The request took too long", "lead": "The server took too long to respond. This can happen with large exports, or when LHDN MyInvois is slow.", "tips": ["Check Invoice Management before you submit again. The request may still have completed.", "For a large export, narrow the date range."]}, "ms": {"title": "Permintaan mengambil masa terlalu lama", "lead": "Pelayan mengambil masa terlalu lama untuk memberi respons. Ini boleh berlaku semasa eksport besar, atau apabila LHDN MyInvois perlahan.", "tips": ["Semak Pengurusan Invois sebelum menghantar semula. Permintaan itu mungkin masih berjaya.", "Untuk eksport besar, kecilkan julat tarikh."]}}, "offline": {"icon": "wifi", "tone": "neutral", "actions": ["reload"], "auto": "online", "en": {"title": "You're offline", "lead": "We can't reach EinvWorld from this device. Check your internet connection. This page reconnects by itself.", "tips": ["Check your Wi-Fi or mobile data.", "Drafts you've already saved are kept on our server."]}, "ms": {"title": "Anda di luar talian", "lead": "Kami tidak dapat mencapai EinvWorld dari peranti ini. Semak sambungan internet anda. Halaman ini akan bersambung semula dengan sendirinya.", "tips": ["Semak Wi-Fi atau data mudah alih anda.", "Draf yang telah disimpan kekal di pelayan kami."]}}}};
  var ICONS = {
    alert:'<path d="M10.3 3.9L1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"/><path d="M12 9v4M12 17h.01"/>',
    lock:'<rect x="4.5" y="11" width="15" height="10" rx="2"/><path d="M8 11V7a4 4 0 0 1 8 0v4M12 15v2"/>',
    shield:'<path d="M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z"/><path d="M9.5 9.5l5 5M14.5 9.5l-5 5"/>',
    search:'<circle cx="11" cy="11" r="7"/><path d="M20 20l-3.5-3.5M8.5 11h5"/>',
    upload:'<path d="M14 3H6a1 1 0 0 0-1 1v16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V8z"/><path d="M14 3v5h5M12 17v-6M9 14l3-3 3 3"/>',
    clock:'<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
    unlink:'<path d="M9 17H7a5 5 0 0 1 0-10h2M15 7h2a5 5 0 0 1 4 8M8 12h3M3 3l18 18"/>',
    cloud:'<path d="M2 2l20 20M5.8 5.8A7 7 0 0 0 7 20h10.5M21 16.5A4.5 4.5 0 0 0 17.5 10h-1A7 7 0 0 0 9.6 5.1"/>',
    hourglass:'<path d="M6 2h12M6 22h12M7 2v4a5 5 0 0 0 10 0V2M7 22v-4a5 5 0 0 1 10 0v4"/>',
    wifi:'<path d="M2 2l20 20M8.5 16.4a5 5 0 0 1 7 0M5 12.9a10 10 0 0 1 4.2-2.6M19 12.9a10 10 0 0 0-2.4-1.7M1.4 9a15 15 0 0 1 3.9-2.5M22.6 9A15 15 0 0 0 10.6 5.1M12 20h.01"/>',
    tip:'<circle cx="12" cy="12" r="9"/><path d="M12 11v5M12 8v.5"/>',
    chev:'<path d="M6 9l6 6 6-6"/>'
  };
  var svg = function(n,c){return '<svg class="'+(c||"")+'" viewBox="0 0 24 24" aria-hidden="true">'+ICONS[n]+'</svg>';};
  var esc = function(s){return String(s).replace(/[&<>"]/g,function(c){return {"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;"}[c];});};
  var C = EW_CONFIG, timers = [], st = {}, lang = "en";
  try{ lang = localStorage.getItem("ew-lang") || localStorage.getItem("mnt-lang") || ((navigator.language||"").slice(0,2)==="ms"?"ms":"en"); }catch(e){}
  var meta = document.querySelector('meta[name="request-id"]');
  var REF = (meta && meta.content && meta.content.indexOf("@")<0) ? meta.content
          : "EW-" + Date.now().toString(36).toUpperCase() + "-" + Math.random().toString(36).slice(2,6).toUpperCase();
  var WHEN = new Date();

  function t(k,v){var s=(D.common[lang][k]||D.common.en[k]);for(var x in v)s=s.split("{"+x+"}").join(v[x]);return s;}
  function clear(){timers.forEach(function(x){clearInterval(x);clearTimeout(x);});timers=[];}
  function when(){try{return WHEN.toLocaleString(lang==="ms"?"ms-MY":"en-MY",{day:"numeric",month:"short",year:"numeric",hour:"numeric",minute:"2-digit",second:"2-digit",timeZone:C.tz})+" MYT";}catch(e){return WHEN.toString();}}
  function href(a){
    if(a==="home") return C.home;
    if(a==="invoices") return C.invoices;
    if(a==="login"||a==="switch") return C.login+"?ReturnUrl="+encodeURIComponent(a==="login"?location.pathname+location.search:"/");
    return "#";
  }
  function details(){ return [t("code")+": "+st.codeLabel, t("time")+": "+when(), t("page")+": "+location.href, t("ref")+": "+REF].join("\n"); }

  function render(){
    var p = D.pages[st.code], L = p[lang] || p.en, off = st.code==="offline";
    st.codeLabel = off ? "offline" : st.code;
    var tips = L.tips.map(function(x){
      if(x==="@urgent") return '<li class="tip">'+svg("tip")+'<span>'+esc(t("urgent")).replace("{myinvois}",'<a href="https://myinvois.hasil.gov.my" target="_blank" rel="noopener">MyInvois portal</a>')+'</span></li>';
      return '<li>'+'<span class="ew-dot" aria-hidden="true"></span><span>'+esc(x)+'</span></li>';
    }).join("");
    var acts = p.actions.map(function(a,i){
      var cls = i===0 ? "ew-btn" : "ew-btn ew-btn2";
      if(a==="back"||a==="reload") return '<button type="button" class="'+cls+'" data-act="'+a+'">'+esc(t(a))+'</button>';
      return '<a class="'+cls+'" href="'+esc(href(a))+'">'+esc(t(a))+'</a>';
    }).join("");
    var mail = '<a href="mailto:'+esc(C.email)+'?subject='+encodeURIComponent("EinvWorld "+st.codeLabel+" "+REF)+'&body='+encodeURIComponent(details()+"\n\n")+'">'+esc(C.email)+'</a>';
    var help = esc(t("help")).replace("{email}", mail + (C.phone?' · <a href="tel:'+C.phone.replace(/[^+\d]/g,"")+'">'+esc(C.phone)+'</a>':"")).replace("{ref}",'<b class="ew-mono">'+esc(REF)+'</b>');
    st.root.className = "ew ew-"+p.tone;
    st.root.innerHTML =
      '<div class="ew-card">'+
        '<div class="ew-top"><span class="ew-logo">'+st.logo+'</span>'+
          '<div class="ew-lang" role="group" aria-label="'+esc(t("lang"))+'"><button type="button" data-lang="en" lang="en" aria-pressed="'+(lang==="en")+'">EN</button><button type="button" data-lang="ms" lang="ms" aria-pressed="'+(lang==="ms")+'">BM</button></div></div>'+
        '<div class="ew-hero"><span class="ew-ico">'+svg(p.icon)+'</span><div>'+
          '<span class="ew-eyebrow">'+esc(off?t("eyebrowOff"):t("eyebrow",{c:st.code}))+'</span>'+
          '<'+st.h+' class="ew-h">'+esc(L.title)+'</'+st.h+'></div></div>'+
        '<p class="ew-lead">'+esc(L.lead.replace("{max}",C.maxUpload))+'</p>'+
        '<div class="ew-what"><h3 class="ew-sub">'+esc(t("whatNow"))+'</h3><ul class="ew-tips">'+tips+'</ul></div>'+
        (p.auto?'<p class="ew-status" role="status" aria-live="polite"><span class="ew-pulse" aria-hidden="true"></span><span class="ew-status-t">'+esc(t("checking"))+'</span></p>':'')+
        '<div class="ew-acts">'+acts+'</div>'+
        '<details class="ew-tech"><summary>'+esc(t("tech"))+svg("chev","ew-chev")+'</summary>'+
          '<dl><dt>'+esc(t("code"))+'</dt><dd class="ew-mono">'+esc(st.codeLabel)+'</dd><dt>'+esc(t("time"))+'</dt><dd>'+esc(when())+'</dd><dt>'+esc(t("page"))+'</dt><dd class="ew-mono ew-url">'+esc(location.href)+'</dd><dt>'+esc(t("ref"))+'</dt><dd class="ew-mono">'+esc(REF)+'</dd></dl>'+
          '<button type="button" class="ew-btn ew-btn2 ew-sm" data-act="copy">'+esc(t("copy"))+'</button></details>'+
      '</div>'+
      '<p class="ew-help">'+help+'</p>'+
      '<p class="ew-foot">© EinvWorld, Datamation Technology (M) Sdn Bhd</p>';
    if(!st.sim) document.title = L.title + " · EinvWorld";
    document.documentElement.lang = lang;
    status();
  }
  function status(msg){
    var el = st.root.querySelector(".ew-status-t"); if(!el) return;
    el.textContent = msg || st.msg || t("checking");
    st.root.querySelector(".ew-status").classList.toggle("up", st.state==="up");
  }
  function setMsg(key,v){ st.msgKey=key; st.msgV=v; st.msg=t(key,v); status(); }
  function auto(){
    var p = D.pages[st.code]; if(!p.auto) return;
    var delays=[15,20,30,45,60,90,120], tries=0;
    function wait(n, key, then){
      var s=n; setMsg(key,{s:s});
      var iv=setInterval(function(){ s--; if(s<=0){clearInterval(iv); then();} else setMsg(key,{s:s}); },1000); timers.push(iv);
    }
    function probe(){
      setMsg("checking");
      if(st.sim){ timers.push(setTimeout(loop,900)); return; }
      if(p.auto==="online" && navigator.onLine===false){ setMsg("offline"); return; }
      var u = location.pathname + (location.search?location.search+"&":"?") + "_ew=" + Date.now();
      fetch(u,{cache:"no-store",credentials:"same-origin"}).then(function(r){
        if(r.ok){ st.state="up"; setMsg("up"); timers.push(setTimeout(function(){location.reload();},700)); } else loop();
      }).catch(loop);
    }
    function loop(){ var d=delays[Math.min(tries++,delays.length-1)]; wait(d, p.auto==="countdown"?"retryIn":"next", p.auto==="countdown"&&!st.sim?function(){location.reload();}:probe); }
    st.probe = function(){ clear(); tries=0; probe(); };
    if(p.auto==="countdown") wait(60,"retryIn",st.sim?loop:function(){location.reload();});
    else timers.push(setTimeout(probe, st.sim?400:3000));
  }
  function onClick(e){
    var lb = e.target.closest("[data-lang]");
    if(lb){ lang=lb.getAttribute("data-lang"); try{localStorage.setItem("ew-lang",lang);}catch(err){} var open=st.root.querySelector(".ew-tech").open; render(); st.root.querySelector(".ew-tech").open=open; if(st.msgKey) setMsg(st.msgKey,st.msgV); return; }
    var b = e.target.closest("[data-act]"); if(!b) return;
    var a = b.getAttribute("data-act");
    if(a==="copy"){ var done=function(){b.textContent=t("copied");setTimeout(function(){b.textContent=t("copy");},1600);};
      try{navigator.clipboard.writeText(details()).then(done,done);}catch(err){done();} return; }
    if(st.sim) return;
    if(a==="back"){ if(history.length>1) history.back(); else location.href=C.home; }
    if(a==="reload"){ if(st.probe && D.pages[st.code].auto==="poll") st.probe(); else location.reload(); }
  }
  return function(o){
    clear(); st = {code:o.code, sim:!!o.sim, root:o.root, logo:o.logo, h:o.h||"h1"};
    if(!o.root._ew){ o.root.addEventListener("click", onClick); o.root._ew=1; }
    if(o.code==="offline" && !o.sim){ window.addEventListener("online", function(){ location.reload(); }); }
    render(); auto();
  };
})();

(function(){
  var root = document.getElementById("ew"); if(!root) return;
  EW({code: root.getAttribute("data-code") || "500", root: root, logo: (document.getElementById("ew-logo") || {}).innerHTML || "", h: "h1"});
})();
