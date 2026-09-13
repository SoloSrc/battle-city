// Review-only composition; runtime CardView is owned by Fable.
const fs=require('fs'),path=require('path'),sharp=require(process.env.SHARP_MODULE||'sharp');
const root=path.resolve(__dirname,'../../..'),out=path.join(root,'docs/art/previews/cards30');
const icon=n=>path.join(root,'assets/cards/icons',n+'.png');
(async()=>{
 fs.mkdirSync(out,{recursive:true});let tiles=[];
 const kinds=['normal','effect','fusion','ritual','spell','trap'];
 const cards=['rogue_doll','sinister_serpent','dark_balter_the_terrible','mystical_elf','book_of_moon','mirror_force'];
 for(let i=0;i<6;i++){
  const kind=kinds[i],layers=[{input:await sharp(path.join(root,'assets/cards/art',cards[i]+'.png')).resize(562,616,{fit:'cover'}).png().toBuffer(),left:14,top:14},{input:path.join(root,'assets/cards/frames/frame_'+kind+'.png'),left:0,top:0}];
  if(i<4){
   const level=[4,1,5,8][i],width=(level-1)*36+34,left=Math.min(295,460-width/2)-width/2;
   for(let j=0;j<level;j++)layers.push({input:await sharp(icon('star')).resize(34,34).png().toBuffer(),left:Math.round(left+j*36),top:679});
   layers.push({input:await sharp(icon('attr_'+['light','water','dark','dark'][i])).resize(64,64).png().toBuffer(),left:482,top:664});
   layers.push({input:Buffer.from(`<svg width="590" height="860"><g font-family="serif" font-style="normal" font-size="70" text-anchor="middle"><text x="158" y="807">${[1600,300,2000,2800][i]}</text><text x="430" y="807">${[1000,250,1200,2600][i]}</text></g></svg>`),left:0,top:0});
  }else layers.push({input:await sharp(icon('st_'+kind)).resize(72,72).png().toBuffer(),left:259,top:714});
  const face=await sharp({create:{width:590,height:860,channels:4,background:'#15151a'}}).composite(layers).png().toBuffer();
  await sharp(face).toFile(path.join(out,kind+'-sample.png'));
  tiles.push({input:await sharp(face).resize(295,430).png().toBuffer(),left:i*315,top:36});
  tiles.push({input:Buffer.from(`<svg width="295" height="30"><text x="148" y="22" fill="white" font-size="18" font-family="sans-serif" text-anchor="middle">${kind.toUpperCase()}</text></svg>`),left:i*315,top:0});
 }
 let names=['attr_light','attr_dark','attr_earth','attr_water','attr_fire','attr_wind','attr_divine','st_spell','st_trap','star','st_continuous','st_equip','st_quick_play','st_counter','st_field','st_ritual'];
 for(let i=0;i<names.length;i++){
  let x=i*116+8;tiles.push({input:await sharp(icon(names[i])).resize(80,80).png().toBuffer(),left:x+14,top:510});
  tiles.push({input:Buffer.from(`<svg width="112" height="45"><text x="56" y="17" fill="white" font-size="11" font-family="sans-serif" text-anchor="middle">${names[i].replace('attr_','').replace('st_','').replace('_',' ')}</text>${i>=10?'<text x="56" y="34" fill="#c9c2b0" font-size="10" font-family="sans-serif" text-anchor="middle">tooltip only</text>':''}</svg>`),left:x,top:595});
 }
 await sharp({create:{width:1890,height:650,channels:4,background:'#252329'}}).composite(tiles).png().toFile(path.join(out,'card-set-review.png'));
 // All attributes and level extremes reviewed independently of card mechanics.
 const rows=[];for(const level of Array.from({length:12},(_,i)=>i+1)){let width=(level-1)*36+34,left=Math.min(295,460-width/2)-width/2;if(left<14||left+width>460)throw Error('Star row clearance');rows.push({level,left,right:left+width,attribute_left:482,gap:482-left-width});}
 fs.writeFileSync(path.join(__dirname,'layout-validation.json'),JSON.stringify({rows,subtypes_on_faces:0,tooltip_subtypes:6,frame_types:6,ritual_sample:'layout fixture only; uses existing symbolic art, not a new card definition'},null,2)+'\n');
 console.log('REVIEW PASS: 6 frame styles; 7 attributes; 6 tooltip-only subtypes; level 1–12 clearance.');
})();
