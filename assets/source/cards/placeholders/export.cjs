// Original SOLOSRC code, MIT. Requires Sharp; composed badges include director reference inputs.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const sharp=require(process.env.SHARP_MODULE||'sharp');
const root=path.resolve(__dirname,'../../../..');
const spec=JSON.parse(fs.readFileSync(path.join(__dirname,'manifest.json')));
const esc=s=>s.replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
function badges(d){ return d.monster?[]:[{name:'st_'+d.kind,x:259,y:714}]; }
function starLeft(level){ const width=(level-1)*36+34;return Math.min(295,460-width/2)-width/2; }
(async()=>{
 const out=path.join(root,'assets/cards/art'),review=path.join(root,'docs/art/previews/card-placeholders');
 fs.mkdirSync(out,{recursive:true});fs.mkdirSync(review,{recursive:true});
 const hashes=new Set(),tiles=[],report=[];let html=[];
 for(let i=0;i<spec.length;i++){
  const card=spec[i],target=path.join(root,card.texture);
  await sharp(path.join(root,card.source)).png().toFile(target);
  const {data,info}=await sharp(target).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  if(info.width!==512||info.height!==512)throw Error(card.id+' size');
  for(let p=3;p<data.length;p+=4)if(data[p]!==255)throw Error(card.id+' is not opaque');
  const hash=crypto.createHash('sha256').update(data).digest('hex');
  if(hashes.has(hash))throw Error(card.id+' duplicate texture');hashes.add(hash);
  report.push({id:card.id,width:512,height:512,opaque:true,sha256:hash});
  const thumb=await sharp(target).resize(128,128).png().toBuffer();
  tiles.push({input:thumb,left:(i%8)*164+18,top:Math.floor(i/8)*174+8});
  const words=card.name.split(' '),lines=[''];for(const word of words){if((lines.at(-1)+' '+word).length>23)lines.push(word);else lines[lines.length-1]+=(lines.at(-1)?' ':'')+word;}
  const text=Buffer.from(`<svg width="164" height="40"><g font-family="sans-serif" font-size="10" fill="#eee" text-anchor="middle">${lines.slice(0,3).map((l,n)=>`<text x="82" y="${12+n*12}">${esc(l)}</text>`).join('')}</g></svg>`);
  tiles.push({input:text,left:(i%8)*164,top:Math.floor(i/8)*174+138});
  const d=JSON.parse(fs.readFileSync(path.join(root,'data/cards',card.id+'.json'))),m=d.monster;
  const category=m?(d.kind==='fusion'?'fusion':m.category==='normal'?'normal':'effect'):d.kind;
  const artwork='../../../../'+card.texture;
  html.push(`<article data-name="${esc(card.name.toLowerCase())}"><div class="card"><img class="art" src="${artwork}" alt="${esc(card.name)} symbolic placeholder"><img class="frame" src="../../../../assets/cards/frames/frame_${category}.png" alt="">${m?`<div class="stars" style="left:${starLeft(m.level)/590*100}%">${Array.from({length:m.level},()=>'<img src="../../../../assets/cards/icons/star.png" alt="star">').join('')}</div><img class="attr" src="../../../../assets/cards/icons/attr_${m.attribute.toLowerCase()}.png" alt="${m.attribute}"><span class="atk">${m.atk}</span><span class="def">${m.def}</span>`:badges(d).map(b=>`<img class="badge" style="left:${b.x/590*100}%;top:${b.y/860*100}%" src="../../../../assets/cards/icons/${b.name}.png" alt="${b.name}">`).join('')}</div><h2>${esc(card.name)}</h2><p>${card.motifs.join(' + ')}</p><a href="${artwork}">512px export</a></article>`);
 }
 await sharp({create:{width:1312,height:Math.ceil(spec.length/8)*174,channels:4,background:'#252329'}}).composite(tiles).png().toFile(path.join(review,'contact-sheet.png'));
 fs.writeFileSync(path.join(__dirname,'validation.json'),JSON.stringify(report,null,2)+'\n');
 fs.writeFileSync(path.join(review,'index.html'),`<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>SOLOSRC · ${spec.length} card placeholders</title><style>body{background:#252329;color:#eee;font:16px system-ui;margin:32px}header{max-width:850px}h1{font-size:28px}input,select{padding:10px;background:#403b42;color:white;border:1px solid #887c82;margin:8px}main{display:grid;grid-template-columns:repeat(auto-fill,minmax(190px,1fr));gap:28px}article{min-width:0}h2{font-size:14px;max-width:230px}p{color:#c6bfc5}article p,article a{font-size:12px}a{color:#e7c895}.card{position:relative;width:177px;height:258px}.card img{position:absolute}.art{left:2.373%;top:1.628%;width:95.254%;height:71.628%;object-fit:cover;background:#28232b}.frame{inset:0;width:100%;height:100%}.stars{position:absolute;top:78.953%;display:flex;gap:.6px}.stars img{position:static;width:10.2px;height:10.2px}.attr{left:81.695%;top:77.209%;width:10.847%;height:7.442%}.badge{width:12.203%;height:8.372%}.atk,.def{position:absolute;top:86.28%;font:normal 21px Georgia;color:#090703;transform:translateX(-50%)}.atk{left:26.78%}.def{left:72.88%}[hidden]{display:none}</style><header><h1>${spec.length} original card placeholders</h1><p>Symbolic silhouettes for gameplay testing. These are not final illustrations. Labels and motif notes belong to this review page only.</p><p>Director reference frame revision 4. Centered cover fills the approved art window without stretching, as agreed with the director. Contain is retained only for comparison. Spell/trap cards show one centered type badge; subtype icons belong only in tooltips. This page is not CardView or a shader acceptance test.</p><label>Find card <input id="search" type="search"></label><label>Preview fit <select id="fit"><option value="cover">Centered cover (approved)</option><option value="contain">Contain (comparison only)</option></select></label></header><main>${html.join('')}</main><script>document.querySelector('#search').oninput=e=>document.querySelectorAll('article').forEach(a=>a.hidden=!a.dataset.name.includes(e.target.value.toLowerCase()));document.querySelector('#fit').onchange=e=>document.querySelectorAll('.art').forEach(a=>a.style.objectFit=e.target.value);</script></html>`);
 for(const [sampleName,samples] of [['frame-samples',['archfiend_soldier','blade_knight','book_of_moon','mirror_force','dark_balter_the_terrible','sinister_serpent']],['rookie-samples',['rogue_doll','celtic_guardian','harpie_lady','feral_imp','koumori_dragon','giant_soldier_of_stone','mystical_elf']]]){
 const framed=[];
 for(let i=0;i<samples.length;i++){
  const id=samples[i],d=JSON.parse(fs.readFileSync(path.join(root,'data/cards',id+'.json'))),m=d.monster;
  const kind=m?(d.kind==='fusion'?'fusion':m.category==='normal'?'normal':'effect'):d.kind;
  const art=await sharp(path.join(out,id+'.png')).resize(562,616,{fit:'cover',position:'centre'}).png().toBuffer();
  const layers=[{input:art,left:14,top:14},{input:path.join(root,'assets/cards/frames/frame_'+kind+'.png'),left:0,top:0}];
  if(m){
   const star=await sharp(path.join(root,'assets/cards/icons/star.png')).resize(34,34).png().toBuffer();
   for(let n=0;n<m.level;n++)layers.push({input:star,left:Math.round(starLeft(m.level)+n*36),top:679});
   layers.push({input:await sharp(path.join(root,'assets/cards/icons/attr_'+m.attribute.toLowerCase()+'.png')).resize(64,64).png().toBuffer(),left:482,top:664});
   layers.push({input:Buffer.from(`<svg width="590" height="860"><g font-family="serif" font-size="70" font-style="normal" text-anchor="middle" fill="#090703"><text x="158" y="807">${m.atk}</text><text x="430" y="807">${m.def}</text></g></svg>`),left:0,top:0});
  }
  for(const b of badges(d))layers.push({input:await sharp(path.join(root,'assets/cards/icons',b.name+'.png')).resize(72,72).png().toBuffer(),left:b.x,top:b.y});
  const full=await sharp({create:{width:590,height:860,channels:4,background:'#252329'}}).composite(layers).png().toBuffer();
  if(id==='rogue_doll'){const folder=path.join(root,'docs/art/previews/duel64');fs.mkdirSync(folder,{recursive:true});await sharp(full).png().toFile(path.join(folder,'rogue-doll-face.png'));}
  framed.push({input:await sharp(full).resize(177,258).png().toBuffer(),left:i*189,top:0});
 }
 await sharp({create:{width:samples.length*189,height:258,channels:4,background:'#252329'}}).composite(framed).png().toFile(path.join(review,sampleName+'.png'));
 }
 console.log(`PASS: ${spec.length} opaque 512×512 PNGs, unique decoded pixel hashes; review page and contact sheet exported.`);
})();
