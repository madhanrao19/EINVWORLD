import { chromium } from '@playwright/test';
const base='http://localhost';
const b=await chromium.launch();
const page=await (await b.newContext()).newPage();
const bad=new Map();
page.on('response',r=>{const u=r.url(); if(u.startsWith(base)&&r.status()>=400){const k=r.status()+' '+u.replace(base,'').split('?')[0]; bad.set(k,(bad.get(k)||0)+1);}});
const paths=[['/','home'],['/login','login'],['/register','register'],['/forgotpassword','forgot'],['/contact','contact'],['/Home/Privacy','privacy'],['/resendemailconfirmation','resend']];
for(const [p,l] of paths){
  const r=await page.goto(base+p,{waitUntil:'networkidle',timeout:30000}).catch(e=>({err:e.message}));
  const title=await page.title().catch(()=>'');
  console.log(`${l.padEnd(9)} ${r?.err?'ERR '+r.err.slice(0,40):r.status()}  "${title.slice(0,40)}"`);
}
console.log('\n--- distinct 4xx/5xx app resources across all pages ---');
if(bad.size===0) console.log('none'); else [...bad.entries()].sort().forEach(([k,n])=>console.log(`${String(n).padStart(3)}x  ${k}`));
await b.close();
