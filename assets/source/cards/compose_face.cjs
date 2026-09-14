const fs=require('fs'),path=require('path'),sharp=require(process.env.SHARP_MODULE||'sharp');
const root=path.resolve(__dirname,'../../..'),L=JSON.parse(fs.readFileSync(path.join(__dirname,'frame-layout.json')));
function starLeft(level){const width=(level-1)*L.star_step+L.star_size[0];return Math.min(L.star_row_center_x,L.star_row_right_max-width/2)-width/2;}
async function compose(art,kind,m){
 const [x,y,w,h]=L.art_window,layers=[{input:await sharp(art).resize(w,h,{fit:'cover'}).png().toBuffer(),left:x,top:y},{input:path.join(root,'assets/cards/frames/frame_'+kind+'.png'),left:0,top:0}];
 async function badge(name,c,size){layers.push({input:await sharp(path.join(root,'assets/cards/icons',name+'.png')).resize(...size).png().toBuffer(),left:Math.round(c[0]-size[0]/2),top:Math.round(c[1]-size[1]/2)});}
 if(m){for(let n=0;n<m.level;n++)await badge('star',[starLeft(m.level)+n*L.star_step+L.star_size[0]/2,L.star_row_y],L.star_size);await badge('attr_'+m.attribute.toLowerCase(),L.attribute,L.attribute_size);
 // SVG baseline approximates optical center for the fixed upright serif preview font.
 layers.push({input:Buffer.from(`<svg width="590" height="860"><g font-family="DejaVu Serif,serif" font-size="${L.stat_font_px}" font-style="normal" text-anchor="middle" fill="#090703"><text x="${L.atk[0]}" y="${L.atk[1]+L.stat_font_px*.35}">${m.atk}</text><text x="${L.def[0]}" y="${L.def[1]+L.stat_font_px*.35}">${m.def}</text></g></svg>`),left:0,top:0});
 }else await badge('st_'+kind,L.spell_trap_badge,L.spell_trap_badge_size);
 return sharp({create:{width:590,height:860,channels:4,background:'#252329'}}).composite(layers).png().toBuffer();
}
module.exports={compose,starLeft,L};
