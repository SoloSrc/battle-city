"""Original SOLOSRC procedural smoke-test assets. MIT. Run with Blender 5.2+."""
import sys, os
if os.environ.get("SMOKE28_PYTHON_DEPS"):
 sys.path.insert(0,os.environ["SMOKE28_PYTHON_DEPS"])
import bpy, math, json
from mathutils import Vector
from pathlib import Path
ROOT=Path(os.environ.get('SMOKE28_REPO', str(Path(__file__).resolve().parents[3])))
SOURCE=ROOT/'assets/source/smoke28'
SOURCE.mkdir(parents=True,exist_ok=True)
for p in ['kit','characters/body','characters/anims','props']:(ROOT/'assets'/p).mkdir(parents=True,exist_ok=True)
REPORT={}
def reset():
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.context.scene.unit_settings.system='METRIC'
 bpy.context.scene.unit_settings.scale_length=1
 bpy.context.scene.render.fps=30

def mat(name,color):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Roughness'].default_value=.85
 return m

def finish(o,name,m,bone=None):
 o.name=name
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if m:o.data.materials.append(m)
 if bone:
  vg=o.vertex_groups.new(name=bone);vg.add(list(range(len(o.data.vertices))),1,'REPLACE')
 return o

def ell(name,c,scale,m,bone=None,seg=16,rings=10):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=seg,ring_count=rings,location=c)
 o=bpy.context.object;o.scale=scale
 for f in o.data.polygons:f.use_smooth=True
 return finish(o,name,m,bone)

def box(name,c,size,m,bone=None,bevel=0):
 bpy.ops.mesh.primitive_cube_add(size=1,location=c);o=bpy.context.object;o.scale=size
 finish(o,name,m,bone)
 if bevel:
  mod=o.modifiers.new('Edge bevel','BEVEL');mod.width=bevel;mod.segments=1
  bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
 return o

def limb(name,a,b,r1,r2,m,bone):
 # Closed tapered capsule-like segment. Overlap at joints avoids cracks in smoke poses.
 a,b=Vector(a),Vector(b);mid=(a+b)*.5
 bpy.ops.mesh.primitive_cone_add(vertices=12,radius1=r1,radius2=r2,depth=(b-a).length+.025,location=mid)
 o=bpy.context.object;o.rotation_mode='QUATERNION';o.rotation_quaternion=(b-a).to_track_quat('Z','Y')
 bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 for f in o.data.polygons:f.use_smooth=True
 return finish(o,name,m,bone)

def rig_make(bones):
 data=bpy.data.armatures.new('Humanoid');arm=bpy.data.objects.new('Rig',data);bpy.context.collection.objects.link(arm)
 bpy.context.view_layer.objects.active=arm;arm.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
 for name,head,tail,parent in bones:
  b=data.edit_bones.new(name);b.head=head;b.tail=tail
  if parent:b.parent=data.edit_bones[parent]
 bpy.ops.object.mode_set(mode='OBJECT');arm.show_in_front=True
 return arm

def skin_join(parts,arm,name):
 bpy.ops.object.select_all(action='DESELECT')
 for p in parts:p.select_set(True)
 bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();o=bpy.context.object;o.name=name
 bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
 o.parent=arm;mod=o.modifiers.new('Humanoid skin','ARMATURE');mod.object=arm
 return o

def selected(objs):
 bpy.ops.object.select_all(action='DESELECT')
 for o in objs:o.select_set(True)
 bpy.context.view_layer.objects.active=objs[0]

def export(path,objs,animations=True):
 selected(objs)
 bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_yup=True,export_apply=True,export_extras=True,export_animations=animations,export_animation_mode='NLA_TRACKS',export_force_sampling=True,export_frame_range=False,export_skins=True,export_all_influences=False)

def triangles(mesh):
 mesh.data.calc_loop_triangles();return len(mesh.data.loop_triangles)

def action(arm,name,seconds,pose):
 arm.animation_data_create();arm.animation_data.action=None
 act=bpy.data.actions.new('anim_'+name);arm.animation_data.action=act
 n=round(seconds*30)
 for f in range(n+1):
  t=f/n
  for b in arm.pose.bones:b.rotation_mode='XYZ';b.rotation_euler=(0,0,0);b.location=(0,0,0)
  pose(arm,t)
  for b in arm.pose.bones:
   b.keyframe_insert(data_path='rotation_euler',frame=f)
   b.keyframe_insert(data_path='location',frame=f)
 arm.animation_data.action=None
 track=arm.animation_data.nla_tracks.new();track.name=name;strip=track.strips.new(name,0,act)
 strip.extrapolation='NOTHING'
 return act

reset()
cube_mat=mat('toon_metric_blue',(.18,.40,.70))
# Bounds min is exactly origin, glTF bounds [0,0,0] to [1,1,1].
cube=box('kit_test_cube_1m',(0.5,-0.5,.5),(1,1,1),cube_mat)
# Godot -col keeps the render mesh and adds collision; use it on the visible cube.
col=cube
cube.name='kit_test_cube_1m-col'
for o in [cube]:
 bpy.context.scene.cursor.location=(0,0,0);bpy.context.view_layer.objects.active=o;bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
cube['asset_id']='0.1';cube['license']='MIT'
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'kit_test_cube_1m.blend'))
export(ROOT/'assets/kit/kit_test_cube_1m.glb',[cube],False)
REPORT['cube']={'metres':[1,1,1],'triangles_visible':triangles(cube),'collision_node':col.name}

reset()
skin=mat('toon_skin',(.64,.39,.25));navy=mat('toon_charcoal',(.045,.065,.105));blue=mat('toon_accent',(.10,.25,.52));ivory=mat('toon_ivory',(.82,.83,.76))
# Blender +Y maps to Godot -Z with standard glTF Y-up conversion.
bones=[('Root',(0,0,0),(0,0,.15),None),('Hips',(0,0,.90),(0,0,1.02),'Root'),('Spine',(0,0,1.02),(0,0,1.17),'Hips'),('Chest',(0,0,1.17),(0,0,1.33),'Spine'),('UpperChest',(0,0,1.33),(0,0,1.39),'Chest'),('Neck',(0,0,1.39),(0,0,1.48),'UpperChest'),('Head',(0,0,1.48),(0,0,1.68),'Neck')]
for side,sgn in [('Left',1),('Right',-1)]:
 pts={'Shoulder':((sgn*.05,0,1.36),(sgn*.18,0,1.36),'UpperChest'),'UpperArm':((sgn*.18,0,1.36),(sgn*.34,0,1.13),side+'Shoulder'),'LowerArm':((sgn*.34,0,1.13),(sgn*.47,0,.93),side+'UpperArm'),'Hand':((sgn*.47,0,.93),(sgn*.51,0,.84),side+'LowerArm'),'UpperLeg':((sgn*.105,0,.92),(sgn*.11,0,.51),'Hips'),'LowerLeg':((sgn*.11,0,.51),(sgn*.11,0,.105),side+'UpperLeg'),'Foot':((sgn*.11,0,.105),(sgn*.11,.14,.055),side+'LowerLeg'),'Toes':((sgn*.11,.14,.055),(sgn*.11,.21,.055),side+'Foot')}
 for nm,(h,t,p) in pts.items():bones.append((side+nm,h,t,p))
arm=rig_make(bones)
parts=[]
parts.append(ell('hips',(0,0,.92),(.17,.105,.14),navy,'Hips'))
parts.append(ell('jacket',(0,0,1.20),(.20,.115,.215),blue,'Chest'))
parts.append(box('shirt_panel',(0,.112,1.21),(.10,.012,.29),ivory,'Chest',.008))
parts.append(ell('neck',(0,0,1.425),(.052,.052,.068),skin,'Neck'))
parts.append(ell('head',(0,.008,1.555),(.095,.09,.124),skin,'Head',20,14))
# Hair cap of custom rings; face remains unobstructed and crown ends at 1.70m.
v=[];faces=[]
for j in range(7):
 theta=(j/6)*1.65
 for i in range(20):
  phi=i/20*math.tau
  # frontal fringe sits higher than the back edge.
  zz=1.577+.123*math.cos(theta)
  if j==6:zz+=.045*max(0,math.sin(phi))
  v.append((.102*math.sin(theta)*math.cos(phi),.008+.097*math.sin(theta)*math.sin(phi),zz))
for j in range(6):
 for i in range(20):a=j*20+i;b=j*20+(i+1)%20;faces.append((a,b,b+20,a+20))
mesh=bpy.data.meshes.new('hair');mesh.from_pydata(v,[],faces);o=bpy.data.objects.new('hair',mesh);bpy.context.collection.objects.link(o)
selected([o]);finish(o,'char_a_hair_01',navy,'Head');parts.append(o)
for sg in [-1,1]:
 parts.append(ell('eye_white',(sg*.038,.088,1.568),(.021,.012,.012),ivory,'Head',12,8))
 parts.append(ell('eye_iris',(sg*.038,.098,1.568),(.008,.006,.010),navy,'Head',10,6))
 parts.append(ell('ear',(sg*.095,0,1.553),(.018,.021,.032),skin,'Head',10,6))
parts.append(ell('nose',(0,.094,1.539),(.012,.017,.017),skin,'Head',10,6))
parts.append(box('mouth',(0,.094,1.509),(.032,.003,.004),navy,'Head'))
for side,sg in [('Left',1),('Right',-1)]:
 d={name:(a,b) for name,a,b,parent in bones}
 for part,r1,r2,m in [('UpperArm',.071,.061,blue),('LowerArm',.056,.039,blue),('UpperLeg',.087,.065,navy),('LowerLeg',.064,.046,navy)]:
  a,b=d[side+part];parts.append(limb(side+part,a,b,r1,r2,m,side+part))
 parts.append(ell(side+'elbow',d[side+'LowerArm'][0],(.058,.055,.06),blue,side+'LowerArm',12,8))
 parts.append(ell(side+'knee',d[side+'LowerLeg'][0],(.064,.061,.067),navy,side+'LowerLeg',12,8))
 parts.append(ell(side+'hand',(sg*.493,.007,.892),(.045,.031,.065),skin,side+'Hand',12,8))
 parts.append(ell(side+'thumb',(sg*.458,.03,.905),(.022,.022,.038),skin,side+'Hand',10,6))
 parts.append(ell(side+'shoe',(sg*.11,.065,.075),(.064,.145,.075),navy,side+'Foot',16,8))
 parts.append(box(side+'sole',(sg*.11,.07,.027),(.12,.255,.032),ivory,side+'Foot',.012))
body=skin_join(parts,arm,'char_a_body')
body['license']='MIT';body['purpose']='Pipeline neutral body A with integrated smoke-only hair and outfit; not final modular parts'
# Local X rotation swings legs in sagittal plane; arms are lowered from A pose using local Z.
def idle(a,t):
 a.pose.bones['Chest'].rotation_euler.x=.012*math.sin(t*math.tau)
 for side,sg in [('Left',1),('Right',-1)]:a.pose.bones[side+'UpperArm'].rotation_euler.z=sg*.50

def walk(a,t):
 phase=t*math.tau
 a.pose.bones['Hips'].location.y=.012*(1-math.cos(phase*2))
 for side,sg in [('Left',1),('Right',-1)]:
  q=phase+(0 if sg==1 else math.pi)
  a.pose.bones[side+'UpperLeg'].rotation_euler.x=.34*math.cos(q)
  a.pose.bones[side+'LowerLeg'].rotation_euler.x=-.48*max(0,math.sin(q))
  a.pose.bones[side+'Foot'].rotation_euler.x=.12*math.sin(q)
  a.pose.bones[side+'UpperArm'].rotation_euler.z=sg*.50
  a.pose.bones[side+'UpperArm'].rotation_euler.x=-.24*math.cos(q)
  a.pose.bones[side+'LowerArm'].rotation_euler.x=-.12

action(arm,'idle',4,idle);action(arm,'walk',1,walk)
for tr in arm.animation_data.nla_tracks:tr.mute=True
bpy.context.scene.frame_start=0;bpy.context.scene.frame_end=120;bpy.context.scene.frame_set(0)
# Bind export in rest; animation file exported with tracks enabled.
arm.data.pose_position='REST'
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'char_a_body.blend'))
export(ROOT/'assets/characters/body/char_a_body.glb',[arm,body],False)
arm.data.pose_position='POSE'
for tr in arm.animation_data.nla_tracks:tr.mute=False
# glTF skeleton-only export requires armature with skinned mesh; reuse mesh in animation carrier.
export(ROOT/'assets/characters/anims/character_anims.glb',[arm,body],True)
REPORT['character']={'triangles':triangles(body),'materials':len(body.data.materials),'bones':[b.name for b in arm.data.bones],'height_m':1.7,'clips':{'idle':4,'walk':1},'source_forward':'+Y','runtime_forward':'-Z','events':{'walk':{'footstep_l':0.0,'footstep_r':0.5}}}
# Save editable scene with animation tracks available but idle solo for preview.
arm.animation_data.nla_tracks.get('walk').mute=True
bpy.context.scene.frame_set(0)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'char_a_body.blend'))

reset()
# Single texture atlas supplies all colours through UV swatches on one toon material.
img=bpy.data.images.new('prop_duel_disk_albedo',width=512,height=512,alpha=True)
colors=[(.83,.86,.88,1),(.10,.15,.22,1),(.18,.45,.70,1),(.48,.83,.94,1)]
pixels=[]
for y in range(512):
 for x in range(512):pixels.extend(colors[min(x//128,3)])
img.pixels.foreach_set(pixels);img.filepath_raw=str(ROOT/'assets/props/prop_duel_disk_albedo.png');img.file_format='PNG';img.save();img.pack()
dm=mat('toon_disk',(.83,.86,.88));bs=dm.node_tree.nodes.get('Principled BSDF');tex=dm.node_tree.nodes.new('ShaderNodeTexImage');tex.image=img;dm.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
arm=rig_make([('DiskRoot',(0,0,0),(0,0,.06),None),('Hinge',(.10,0,.08),(.10,0,.18),'DiskRoot')]);arm.name='DiskRig'
parts=[]
def diskpart(o,idx):
 uv=o.data.uv_layers.active or o.data.uv_layers.new(name='UVMap')
 for loop in uv.data:loop.uv=((idx+.5)/4,.5)
 parts.append(o);return o
# Strap centre = origin. Blender Z is dorsal/up. Blade deployed along +X.
diskpart(ell('hub',(0,0,.06),(.125,.14,.060),dm,'DiskRoot',20,8),0)
diskpart(ell('hub_inset',(0,0,.111),(.082,.085,.012),dm,'DiskRoot',16,6),1)
diskpart(box('strap',(0,0,-.02),(.14,.22,.035),dm,'DiskRoot',.006),1)
diskpart(box('counter',(0,-.08,.13),(.115,.045,.025),dm,'DiskRoot',.005),1)
diskpart(box('counter_display',(0,-.08,.144),(.09,.029,.003),dm,'DiskRoot'),3)
diskpart(box('deck_holder',(-.09,.08,.10),(.09,.115,.06),dm,'DiskRoot',.006),0)
diskpart(box('deck_slot',(-.09,.08,.133),(.066,.090,.008),dm,'DiskRoot'),1)
diskpart(box('graveyard',(-.09,-.075,.07),(.09,.075,.05),dm,'DiskRoot',.006),0)
diskpart(box('grave_slot',(-.09,-.075,.099),(.065,.055,.004),dm,'DiskRoot'),1)
# Swept blade polygon, 0.62 m extent from hinge, recessed bays emphasized by inset faces.
poly=[(.08,-.13),(.26,-.14),(.47,-.06),(.70,.10),(.74,.22),(.50,.12),(.28,.025),(.08,.025)]
v=[(x,y,z) for z in [.07,.095] for x,y in poly];n=len(poly);fs=[tuple(reversed(range(n))),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
me=bpy.data.meshes.new('blade');me.from_pydata(v,[],fs);ob=bpy.data.objects.new('swept_blade',me);bpy.context.collection.objects.link(ob);selected([ob]);finish(ob,'swept_blade',dm,'Hinge');diskpart(ob,0)
positions=[]
for i in range(5):
 x=.17+i*.115;y=-.07+max(0,i-1)*.051
 positions.append((x,y,.108))
 diskpart(box('bay_rim_'+str(i+1),(x,y,.100),(.104,.115,.011),dm,'Hinge'),1)
 diskpart(box('bay_'+str(i+1),(x,y,.107),(.089,.099,.004),dm,'Hinge'),2)
 diskpart(box('bay_light_'+str(i+1),(x,y+.046,.110),(.075,.003,.002),dm,'Hinge'),3)
disk=skin_join(parts,arm,'prop_duel_disk')
# Non-mesh nodes follow their bones and survive export as named anchor nodes.
markers=[]
for name,pos,bone in [('deck',(-.09,.08,.14),'DiskRoot'),('graveyard',(-.09,-.075,.11),'DiskRoot'),('banished',(-.15,-.10,.12),'DiskRoot')]+[('bay_'+str(i+1),p,'Hinge') for i,p in enumerate(positions)]:
 o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);o.empty_display_type='ARROWS';o.empty_display_size=.03
 o.parent=arm;o.parent_type='BONE';o.parent_bone=bone
 # Blender bone parenting origin is tail; preserve desired world location.
 bpy.context.view_layer.update();o.matrix_world.translation=Vector(pos)
 markers.append(o)
def deploy(a,t):a.pose.bones['Hinge'].rotation_euler.y=math.radians(100)*(1-t)
def fold(a,t):a.pose.bones['Hinge'].rotation_euler.y=math.radians(100)*t
action(arm,'disk_deploy',.6,deploy);action(arm,'disk_fold',.6,fold)
arm.animation_data.nla_tracks.get('disk_fold').mute=True
bpy.context.scene.frame_start=0;bpy.context.scene.frame_end=18;bpy.context.scene.frame_set(18)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'prop_duel_disk.blend'))
for tr in arm.animation_data.nla_tracks:tr.mute=False
export(ROOT/'assets/props/prop_duel_disk.glb',[arm,disk]+markers,True)
REPORT['disk']={'triangles':triangles(disk),'materials':len(disk.data.materials),'origin':'forearm strap centre','deployed_axis':'+X','markers':[o.name for o in markers],'clips':{'disk_deploy':.6,'disk_fold':.6},'texture':[512,512]}
(SOURCE/'build_report.json').write_text(json.dumps(REPORT,indent=2)+'\n')
assert REPORT['character']['triangles']<=12000,REPORT['character']['triangles']
assert REPORT['character']['materials']<=4
assert REPORT['disk']['triangles']<=1500,REPORT['disk']['triangles']
print('SMOKE28 COMPLETE',json.dumps(REPORT))
