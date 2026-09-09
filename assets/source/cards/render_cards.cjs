// SOLOSRC MIT. Requires sharp; rasterizes original SVG sources without font dependencies.
const fs=require('fs'),path=require('path');
const sharp=require(process.env.SHARP_MODULE || 'sharp');
const root=process.env.CARDS_REPO||path.resolve(__dirname,'../../..');
const src=path.join(root,'assets/source/cards');
(async()=>{
 let report=[];
 for(const f of fs.readdirSync(src).filter(x=>x.endsWith('.svg'))){
  const stem=f.slice(0,-4),dir=stem.startsWith('frame_')||stem==='card_back'?'frames':'icons';
  const out=path.join(root,'assets/cards',dir,stem+'.png');
  await sharp(path.join(src,f)).png().toFile(out);
  const {data,info}=await sharp(out).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  const frame=stem.startsWith('frame_'),size=frame||stem==='card_back'?[590,860]:stem==='star'?[64,64]:[128,128];
  if(info.width!==size[0]||info.height!==size[1])throw Error(stem+' dimensions');
  if(frame){for(let y=40;y<600;y++)for(let x=35;x<555;x++)if(data[(y*590+x)*4+3]!==0)throw Error(stem+' window has opaque pixels '+x+','+y);}
  if(stem==='card_back'){
   let worst=0,total=0;for(let i=0;i<data.length;i+=4)for(let c=0;c<4;c++){const delta=Math.abs(data[i+c]-data[data.length-4-i+c]);worst=Math.max(worst,delta);total+=delta;}
   if(total/data.length>0.75)throw Error('Back rotational symmetry mean error '+total/data.length); // Subpixel antialiasing can differ along matching edges.
  }
  report.push({asset:stem,size,art_window_clear:frame?true:undefined});
 }
 fs.writeFileSync(path.join(src,'validation.json'),JSON.stringify(report,null,2)+'\n');
 console.log('Validated '+report.length+' PNGs: dimensions, transparent windows, back symmetry');
})();
