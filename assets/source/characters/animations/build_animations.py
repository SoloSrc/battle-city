"""SOLOSRC original animation blocking, MIT. Blender 5.2, unchanged shared rig.
Run from the repository root. Source body is the approved pipeline carrier;
this builder writes only the animation carrier, editable source and manifest.
"""
import os, sys, math, json
from pathlib import Path
if os.environ.get('SMOKE28_PYTHON_DEPS'):
    sys.path.insert(0, os.environ['SMOKE28_PYTHON_DEPS'])
import bpy
from mathutils import Vector

ROOT = Path.cwd()
OUT = ROOT/'assets/source/characters/animations'
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'assets/source/smoke28/char_a_body.blend'))
arm = bpy.data.objects['Rig']
body = bpy.data.objects['char_a_body']
arm.animation_data_clear()
arm.data.pose_position = 'POSE'
bpy.context.scene.render.fps = 30
bpy.context.scene.render.fps_base = 1

def ease(t):
    t=max(0,min(1,t)); return t*t*(3-2*t)
def pulse(t, start, peak, end):
    return ease((t-start)/(peak-start)) if t<peak else 1-ease((t-peak)/(end-peak))
def rot(name,x=0,y=0,z=0): arm.pose.bones[name].rotation_euler=(x,y,z)
def neutral(t=0):
    rot('LeftUpperArm',z=.5); rot('RightUpperArm',z=-.5)
    rot('Chest',x=.012*math.sin(t*math.tau))

def aim(name, end):
    """Aim a segment in armature space, retaining its rest-pose roll."""
    bone=arm.pose.bones[name]
    head=bone.head.copy()
    direction=Vector(end)-head
    rest=bone.bone.tail_local-bone.bone.head_local
    q=rest.rotation_difference(direction) @ bone.bone.matrix_local.to_quaternion()
    mat=q.to_matrix().to_4x4();mat.translation=head;bone.matrix=mat
    bpy.context.view_layer.update()

def arm_to(side, wrist):
    bpy.context.view_layer.update()
    upper=arm.pose.bones[side+'UpperArm']; lower=arm.pose.bones[side+'LowerArm']
    head=upper.head.copy(); target=Vector(wrist); v=target-head
    a,b=upper.bone.length,lower.bone.length
    d=max(.06,min(v.length,a+b-.005)); direction=v.normalized()
    target=head+direction*d
    along=(a*a-b*b+d*d)/(2*d)
    height=math.sqrt(max(0,a*a-along*along))
    hint=Vector((1 if side=='Left' else -1,-.2,-.6))
    bend=(hint-direction*hint.dot(direction)).normalized()
    elbow=head+direction*along+bend*height
    aim(side+'UpperArm',elbow); aim(side+'LowerArm',target)

LEFT=Vector((.30,.20,1.15)); RIGHT=Vector((-.21,.20,1.10))
def duel(t=0):
    neutral(t)
    rot('Chest',x=.012*math.sin(t*math.tau),z=.035)
    arm_to('Left',LEFT)
    arm_to('Right',RIGHT)
def gait(t, running=False):
    neutral(t)
    p=t*math.tau
    arm.pose.bones['Hips'].location.y=(.025 if running else .012)*(1-math.cos(2*p))
    rot('Chest',x=(-.10 if running else -.025),y=.035*math.sin(p))
    for side,sign in [('Left',1),('Right',-1)]:
        q=p+(0 if sign==1 else math.pi)
        rot(side+'UpperLeg',x=(.85 if running else .50)*math.cos(q))
        rot(side+'LowerLeg',x=-(1.10 if running else .70)*max(0,math.sin(q)))
        rot(side+'Foot',x=.16*math.sin(q))
        rot(side+'UpperArm',x=-(.55 if running else .3)*math.cos(q),z=sign*.5)
        rot(side+'LowerArm',x=-.65 if running else -.15)

def pose(name,t):
    if name=='idle': neutral(t)
    elif name in ('walk','run'): gait(t,name=='run')
    elif name.startswith('turn_'):
        neutral(t); side='Left' if name=='turn_l' else 'Right'; sign=1 if side=='Left' else -1
        q=math.sin(math.pi*t)**2
        rot('Hips',y=sign*.14*q);rot('Chest',y=sign*.10*q)
        rot(side+'UpperLeg',x=.12*q);rot(side+'LowerLeg',x=-.24*q)
    elif name=='talk':
        neutral(t);q=math.sin(math.pi*t)**2
        rot('Head',x=.05*math.sin(4*math.pi*t)*q)
        arm_to('Right',Vector((-.35,.04,.94)).lerp(Vector((-.30,.25,1.19)),q))
    elif name=='duel_ready':
        neutral(t);q=ease(t/.42)
        arm_to('Left',Vector((.35,.03,.95)).lerp(LEFT,q))
        arm_to('Right',Vector((-.35,.03,.95)).lerp(RIGHT,ease((t-.18)/.65)))
    elif name=='duel_idle': duel(t)
    elif name in ('draw_card','play_card','card_to_grave'):
        duel(t)
        seconds={'draw_card':.8,'play_card':.9,'card_to_grave':.6}[name]
        event={'draw_card':.4,'play_card':.5,'card_to_grave':.3}[name]/seconds
        target={'draw_card':Vector((.24,.24,1.23)),
                'play_card':Vector((-.15,.44,1.25)),
                'card_to_grave':Vector((.20,.25,1.18))}[name]
        arm_to('Right',RIGHT.lerp(target,pulse(t,0,event,1)))
        rot('Head',x=.06*pulse(t,0,event,1))
    elif name=='take_damage':
        duel(t);q=pulse(t,0,.1/.7,1)
        rot('Chest',x=.18*q);rot('Head',x=.10*q)
        arm.pose.bones['Hips'].location.y=-.025*q
    elif name=='win':
        duel(t);q=pulse(t,0,.38,1)
        arm_to('Right',RIGHT.lerp(Vector((-.28,.05,1.82)),q))
        rot('Head',x=-.07*q)
    elif name=='lose':
        duel(t);q=pulse(t,0,.40,1)
        rot('Chest',x=-.16*q);rot('Head',x=.18*q)
        arm_to('Right',RIGHT.lerp(Vector((-.32,.02,.98)),q))

clips={'idle':4,'walk':1,'run':.7,'turn_l':.3,'turn_r':.3,'talk':3,
       'duel_ready':1.2,'draw_card':.8,'play_card':.9,'card_to_grave':.6,
       'take_damage':.7,'win':2.5,'lose':2.5,'duel_idle':4}
loops={'idle','walk','run','talk','duel_idle'}
for name,seconds in clips.items():
    arm.animation_data_create()
    act=bpy.data.actions.new('anim_'+name);arm.animation_data.action=act
    frames=round(seconds*30)
    loop_start = {}
    for frame in range(frames+1):
        for bone in arm.pose.bones:
            bone.rotation_mode='XYZ';bone.rotation_euler=(0,0,0);bone.location=(0,0,0);bone.scale=(1,1,1)
        bpy.context.view_layer.update()
        pose(name,frame/frames)
        if name in ('walk','run'):
            # Keep the lower sole at the floor; the run retains a small flight arc.
            bpy.context.view_layer.update()
            evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get())
            mesh=evaluated.to_mesh()
            low=min((evaluated.matrix_world @ v.co).z for v in mesh.vertices)
            evaluated.to_mesh_clear()
            flight=.04*abs(math.sin(frame/frames*math.tau*2)) if name=='run' else 0
            arm.pose.bones['Hips'].location.y += .011+flight-low
        # Bake identical endpoints, including solved arm transforms.
        if frame == 0 and name in loops:
            loop_start = {b.name: (b.location.copy(), b.rotation_euler.copy()) for b in arm.pose.bones}
        elif frame == frames and name in loops:
            for bone in arm.pose.bones:
                bone.location, bone.rotation_euler = loop_start[bone.name]
        for bone in arm.pose.bones:
            bone.keyframe_insert('rotation_euler',frame=frame)
            bone.keyframe_insert('location',frame=frame)
    arm.animation_data.action=None
    track=arm.animation_data.nla_tracks.new();track.name=name
    strip=track.strips.new(name,0,act);strip.extrapolation='NOTHING'
    print('AUTHORED',name,frames,'frames')

bpy.context.scene.frame_start=0;bpy.context.scene.frame_end=120
bpy.context.scene.frame_set(0)
bpy.ops.object.select_all(action='DESELECT');arm.select_set(True);body.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.gltf(filepath=str(ROOT/'assets/characters/anims/character_anims.glb'),
    export_format='GLB',use_selection=True,export_yup=True,export_apply=True,
    export_extras=True,export_animations=True,export_animation_mode='NLA_TRACKS',
    export_force_sampling=True,export_frame_range=False,export_skins=True)
for track in arm.animation_data.nla_tracks:track.mute=track.name!='duel_idle'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'character_anims.blend'))
(OUT/'manifest.json').write_text(json.dumps({'license':'MIT','fps':30,'root_motion':False,
    'stage':'authored motion blockout; director review pending',
    'rig_source':'assets/source/smoke28/char_a_body.blend',
    'clips':{k:{'seconds':v,'loop':k in loops} for k,v in clips.items()},
    'events_source':'data/rig/animation_events.json'},indent=2)+'\n')
