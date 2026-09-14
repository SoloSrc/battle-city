// Review-only composition; runtime CardView is owned by Fable.
const fs=require('fs'),path=require('path'),sharp=require(process.env.SHARP_MODULE||'sharp');
const root=path.resolve(__dirname,'../../..'),out=path.join(root,'docs/art/previews/cards30');
const {compose,L,starLeft}=require('./compose_face.cjs');
const icon=n=>path.join(root,'assets/cards/icons',n+'.png');
(async()=>{
 fs.mkdirSync(out,{recursive:true});let tiles=[];
 const kinds=['normal','effect','fusion','ritual','spell','trap'];
 const cards=['rogue_doll','sinister_serpent','dark_balter_the_terrible','mystical_elf','book_of_moon','mirror_force'];
 for(let i=0;i<6;i++){
  const kind=kinds[i];
  const face=await compose(path.join(root,'assets/cards/art',cards[i]+'.png'),kind,i<4?{level:[4,1,12,8][i],attribute:['light','water','dark','dark'][i],atk:[1600,300,4500,2800][i],def:[1000,250,3800,2600][i]}:null);
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
 const rows=[];for(let level=1;level<=12;level++){const width=(level-1)*L.star_step+L.star_size[0],left=starLeft(level),attrLeft=L.attribute[0]-L.attribute_size[0]/2;if(left<20||left+width>attrLeft-20)throw Error('Star row clearance');rows.push({level,left,right:left+width,attribute_left:attrLeft,gap:attrLeft-left-width});}
 const [px,py,pw,ph]=L.spell_trap_plate,[dx,dy,dw,dh]=L.data_panel;
 if(px+pw/2!==dx+dw/2||py+ph/2!==dy+dh/2)throw Error('Spell/trap centering');
 if(L.attribute[1]!==L.star_row_y||L.attribute[1]+L.attribute_size[1]/2>=L.stat_plates[0][1]-6)throw Error('Attribute vertical clearance');
 fs.writeFileSync(path.join(__dirname,'layout-validation.json'),JSON.stringify({rows,subtypes_on_faces:0,tooltip_subtypes:6,frame_types:6,ritual_sample:'layout fixture only; uses existing symbolic art, not a new card definition'},null,2)+'\n');
 console.log('REVIEW PASS: 6 frame styles; 7 attributes; 6 tooltip-only subtypes; level 1–12 clearance.');
})();
