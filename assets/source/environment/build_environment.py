"""Original SOLOSRC MIT greybox environment kit. Blender 5.2+."""
import os,sys
if os.environ.get('SMOKE28_PYTHON_DEPS'):sys.path.insert(0,os.environ['SMOKE28_PYTHON_DEPS'])
import bpy,math,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(os.environ.get('KIT_REPO',str(Path(__file__).resolve().parents[3])))
SRC=ROOT/'assets/source/environment';SRC.mkdir(parents=True,exist_ok=True)
REPORT=[]
COLORS={'stone':(.66,.66,.61),'plaster':(.83,.77,.63),'trim':(.9,.88,.78),'road':(.31,.35,.42),'grass':(.32,.49,.25),'path':(.66,.56,.4),'blue':(.16,.32,.56),'glass':(.25,.46,.53),'wood':(.37,.25,.15),'metal':(.17,.20,.24),'warm':(.64,.30,.18),'green':(.23,.42,.28),'water':(.24,.52,.66),'light':(.95,.82,.40)}
def start():
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
 global M;M={}
 for n,c in COLORS.items():
  m=bpy.data.materials.new('toon_'+n);m.diffuse_color=(*c,1);m.use_nodes=True;m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*c,1);m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.9;M[n]=m
# Author in Godot coordinates x, depth, height; Blender export maps -depth to +Z.
def box(x,d,z,w,l,h,mat='stone'):
 bpy.ops.mesh.primitive_cube_add(size=1,location=(x+w/2,-d-l/2,z+h/2));o=bpy.context.object;o.scale=(w,l,h);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(M[mat]);return o
def cylinder(x,d,z,r,h,mat='stone',sides=12):
 bpy.ops.mesh.primitive_cylinder_add(vertices=sides,radius=r,depth=h,location=(x,-d,z+h/2));o=bpy.context.object;o.data.materials.append(M[mat]);return o
def cone(x,d,z,r,h,mat='grass'):
 bpy.ops.mesh.primitive_cone_add(vertices=10,radius1=r,radius2=r*.24,depth=h,location=(x,-d,z+h/2));bpy.context.object.data.materials.append(M[mat])
def wedge(w,l,h,mat):
 # Ramp from z=0 at d=0 to z=h at d=l, exact convex boundary.
 v=[(0,0,0),(w,0,0),(w,-l,0),(0,-l,0),(w,-l,h),(0,-l,h)];f=[(0,3,2,1),(0,1,4,5),(1,2,4),(3,5,4,2),(0,5,3)]
 mesh=bpy.data.meshes.new('wedge');mesh.from_pydata(v,[],f);mesh.update();o=bpy.data.objects.new('wedge',mesh);bpy.context.collection.objects.link(o);o.data.materials.append(M[mat]);return o

def deliver(name,build,collision=True,note='',group='kit'):
 start();build();objs=[o for o in bpy.context.scene.objects if o.type=='MESH'];bpy.ops.object.select_all(action='DESELECT')
 for o in objs:o.select_set(True)
 bpy.context.view_layer.objects.active=objs[0];bpy.ops.object.join();o=bpy.context.object;o.name=name+('-col' if collision else '');bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
 # Recalculate manifold primitive normals after joins.
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
 pts=[o.matrix_world@Vector(v) for v in o.bound_box];mins=[min(v[i] for v in pts) for i in range(3)];maxs=[max(v[i] for v in pts) for i in range(3)]
 size=[round(maxs[0]-mins[0],4),round(maxs[2]-mins[2],4),round(maxs[1]-mins[1],4)]
 o.data.calc_loop_triangles();record={'name':name,'path':f'assets/{group}/{name}.glb','size_xyz_m':size,'collision':collision,'triangles':len(o.data.loop_triangles),'note':note};REPORT.append(record)
 out=ROOT/'assets'/group;out.mkdir(parents=True,exist_ok=True)
 bpy.ops.wm.save_as_mainfile(filepath=str(SRC/(name+'.blend')))
 bpy.ops.export_scene.gltf(filepath=str(out/(name+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_animations=False,export_materials='EXPORT',export_cameras=False,export_lights=False)

for kind,mat in [('street','road'),('pavement','stone'),('plaza','trim'),('grass','grass'),('path','path')]:
 for suffix,w in [('',2),('_edge',1)]:deliver('kit_ground_'+kind+suffix,lambda w=w,mat=mat:box(0,0,0,w,2,.25,mat),note='Place at Y=-0.25 for a finished surface at Y=0.')
deliver('kit_kerb_straight',lambda:box(0,0,0,2,.25,.25))
deliver('kit_kerb_corner_out',lambda:(box(0,0,0,2,.25,.25),box(0,.25,0,.25,1.75,.25)))
deliver('kit_kerb_corner_in',lambda:(box(0,1.75,0,2,.25,.25),box(1.75,0,0,.25,1.75,.25)))
deliver('kit_kerb_ramp',lambda:wedge(2,1,.25,'stone'),note='1:4 slope; connect street at Y=0 to pavement at Y=0.25.')
def wall(kind):
 if kind=='door':
  box(0,0,0,1,.25,3.5,'plaster');box(3,0,0,1,.25,3.5,'plaster');box(1,0,2.5,2,.25,1,'plaster')
 elif kind=='corner':box(0,0,0,2,.25,3.5,'plaster');box(0,.25,0,.25,1.75,3.5,'plaster')
 else:
  box(0,0,0,2,.25,3.5,'plaster')
  if kind in ('window','shop_front'):
   box(.25,.25,1,1.5,.125,1.5,'trim');box(.375,.375,1.125,1.25,.0625,1.25,'glass')
   box(.9375,.4375,1.125,.125,.0625,1.25,'trim')
for kind in ['plain','window','door','shop_front','corner']:deliver('kit_wall_'+kind,lambda k=kind:wall(k),note='4 m module with 2 m clear opening.' if kind=='door' else '3.5 m storey; place front details toward +Z.')
def roof(kind,mat):
 if kind=='flat':box(0,0,0,2,2,.25,mat)
 elif kind=='ridge':box(0,0,0,2,.25,.25,mat)
 else:
  a=wedge(2,1,1,mat);b=wedge(2,1,1,mat);b.rotation_euler[2]=math.pi;b.location=(2,-2,0)
  if kind=='pitched_end':box(0,0,0,.125,2,.125,'trim')
for mat in ['warm','green']:
 for kind in ['flat','pitched','pitched_end','ridge']:deliver('kit_roof_'+kind+'_'+mat,lambda k=kind,m=mat:roof(k,m))
deliver('kit_shop_awning',lambda:(box(0,0,0,4,1,.25,'blue'),box(0,.875,0,4,.125,.5,'blue')),note='Origin is awning lower bound; place above doorway.')
deliver('kit_shop_sign',lambda:(box(0,0,0,2,.25,1,'wood'),box(.125,.25,.125,1.75,.0625,.75,'blue')),note='Blank sign surface; original sign graphics come in dressing pass.')
deliver('kit_shop_door',lambda:(box(0,0,0,2,.125,2.5,'wood'),box(.125,.125,.625,1.75,.0625,1.625,'glass')),note='Separate removable door leaf; never merge into the portal wall.')
deliver('kit_shop_window_display',lambda:(box(0,0,0,2,.5,.5,'wood'),box(0,0,.5,2,.125,1.5,'glass'),box(.25,.125,.5,.5,.25,.75,'trim'),box(1.25,.125,.5,.5,.25,.75,'blue')))
def arcade_sign():
 box(0,0,0,4,.25,1,'metal');box(.25,.25,.25,3.5,.125,.5,'warm')
 for x in [.125,1,2,3,3.75]:box(x,.25,.8125,.125,.125,.125,'light')
deliver('kit_arcade_sign',arcade_sign,note='Blank central sign region; light blocks are non-emissive greybox markers.')
deliver('kit_arcade_marquee',lambda:(box(0,0,0,4,1,.5,'warm'),box(.25,1,.125,3.5,.125,.25,'light')))
deliver('kit_arcade_closed_doors',lambda:(box(0,0,0,2,.25,2.5,'metal'),box(2,0,0,2,.25,2.5,'metal'),box(1.875,.25,.75,.25,.125,1,'warm')))
deliver('kit_bound_hedge',lambda:box(0,0,0,2,.75,1.5,'grass'))
deliver('kit_bound_fence',lambda:(box(0,0,0,.125,.25,1.5,'metal'),box(1.875,0,0,.125,.25,1.5,'metal'),box(0,0,.5,2,.125,.125,'metal'),box(0,0,1.125,2,.125,.125,'metal')))
deliver('kit_bound_construction',lambda:(box(0,0,0,.25,.75,.5,'metal'),box(1.75,0,0,.25,.75,.5,'metal'),box(0,.25,.5,2,.25,.75,'warm')))
deliver('kit_bound_river_edge',lambda:(box(0,0,0,2,.5,.5,'stone'),box(0,0,.5,.125,.25,1,'metal'),box(1.875,0,.5,.125,.25,1,'metal'),box(0,0,1.25,2,.125,.25,'metal')),note='Solid edging plus rail; continuous rail blocks stepping into river.')
deliver('kit_bound_river_water',lambda:box(0,0,0,2,2,.0625,'water'),collision=False,note='Visual surface only. Pair with river-edge collision and progression boundaries.')
def bench():
 box(0,0,.5,2,.75,.125,'wood');box(0,0,.875,2,.125,.625,'wood')
 for x in [.25,1.625]:box(x,.125,0,.125,.5,.5,'metal')
deliver('prop_bench',bench,group='props')
deliver('prop_lamp_post',lambda:(box(0,0,0,.5,.5,.25,'metal'),cylinder(.25,.25,.25,.08,2.75,'metal'),box(0,0,3,.5,.5,.5,'light')),group='props')
for name,sz in [('small',1),('large',2)]:
 deliver('prop_planter_'+name,lambda sz=sz:(box(0,0,0,sz,sz,.5,'stone'),box(.125,.125,.5,sz-.25,sz-.25,.25,'grass')),group='props')
for name,r,h in [('round',1,3.5),('tall',.75,4.5)]:
 deliver('prop_tree_'+name,lambda r=r,h=h:(cylinder(r,r,0,.16,1.5,'wood'),cone(r,r,1.5,r,h-1.5)),group='props',note='Greybox canopy mass; collision intentionally conservative until dressing.')
deliver('prop_bush',lambda:cone(.5,.5,0,.5,1),group='props')
def fountain():
 # Ring basin leaves a recessed interior; no stacked solid disks filling it.
 for j in range(16):
  a=j*2*math.pi/16;x=2+1.75*math.cos(a);d=2+1.75*math.sin(a)
  o=box(x-.375,d-.1875,0,.75,.375,.5,'stone');o.rotation_euler[2]=-a-math.pi/2
 cylinder(2,2,.125,1.6,.0625,'water',32);cylinder(2,2,.1875,.25,1.25,'stone');cylinder(2,2,1.4375,.625,.125,'trim')
deliver('prop_fountain',fountain,group='props',note='Basin and pedestal blockout. No water VFX; outside 16×12 duel clearances.')
deliver('prop_sign_post',lambda:(box(0,0,0,.25,.25,2,'wood'),box(0,0,1.5,1,.125,.5,'blue')),group='props')
deliver('prop_bin',lambda:cylinder(.375,.375,0,.375,1,'metal'),group='props')
deliver('prop_crate',lambda:(box(0,0,0,1,1,1,'wood'),box(0,0,0,1,.125,.125,'trim'),box(0,0,.875,1,.125,.125,'trim')),group='props')
deliver('kit_int_counter',lambda:(box(0,0,0,2,.75,1,'wood'),box(0,0,1,2,1,.125,'trim')))
def shelves():
 box(0,0,0,.125,.5,2);box(1.875,0,0,.125,.5,2)
 for z in [0,.625,1.25,1.875]:box(0,0,z,2,.5,.125,'wood')
deliver('kit_int_shelves',shelves)
deliver('kit_int_card_display',lambda:(box(0,0,0,1,.75,.875,'wood'),box(0,0,.875,1,.75,.125,'glass')))
deliver('kit_int_floor',lambda:box(0,0,0,2,2,.25,'wood'))
deliver('kit_int_wall',lambda:box(0,0,0,2,.25,3.5,'plaster'))
deliver('kit_int_bed',lambda:(box(0,0,0,1,2,.375,'wood'),box(0,0,.375,1,2,.25,'trim'),box(0,0,.625,1,.5,.125,'blue')))
deliver('kit_int_desk',lambda:(box(0,0,.75,1.5,.75,.125,'wood'),box(0,0,0,.125,.75,.75,'metal'),box(1.375,0,0,.125,.75,.75,'metal')))
deliver('kit_int_door',lambda:box(0,0,0,2,.125,2.5,'wood'),note='Separate leaf. Use kit_wall_door for the 2 m clear portal.')
(SRC/'manifest.json').write_text(json.dumps(REPORT,indent=2)+'\n');print('DELIVERED',len(REPORT),'assets')
