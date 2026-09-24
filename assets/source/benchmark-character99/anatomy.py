"""Revision 2: complete anatomical body and fitted garment surfaces.
Executed by build.py before scene normalization; all dimensions are metres.
"""
from math import exp, atan2, sqrt
import bmesh

def remove_prefix(prefixes):
    for ob in list(model.objects):
        if ob.name.startswith(tuple(prefixes)):bpy.data.objects.remove(ob,do_unlink=True)
remove_prefix(['Head','Neck','Nose bridge','Mouth','Lower lip','L ear','R ear','Ear inner fold',
               'Eye white','Iris','Pupil','Eye glint','Upper eyelid','Lower eyelid','Eyebrow',
               'Hand palm','Finger','Thumb','Trousers','Front pocket','Charcoal shirt','Shirt neckline'])
body_parts=[]
def normalise_normals(ob):
    bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free()
def fused(objects,name,voxel=.0018):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:
        ob.select_set(True);bpy.context.view_layer.objects.active=ob
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        for mod in list(ob.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
        normalise_normals(ob)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();ob=bpy.context.object;ob.name=name
    ob.data.remesh_voxel_size=voxel;ob.data.remesh_voxel_adaptivity=0
    bpy.ops.object.voxel_remesh()
    sm=ob.modifiers.new('Anatomical surface relaxation','SMOOTH');sm.factor=.75;sm.iterations=3
    bpy.ops.object.modifier_apply(modifier=sm.name)
    dec=ob.modifiers.new('Sculpt source density','DECIMATE');dec.ratio=.12
    bpy.ops.object.modifier_apply(modifier=dec.name)
    for face in ob.data.polygons:face.use_smooth=True
    return ob
# Lower body: hip, knee and ankle lie on a straight, slightly spread axis in front view.
# Lateral flesh profiles surround that axis; the patella faces forward, not inward.
legrows=[(.060,.132,.010,.028,.035,.038),(.095,.131,.010,.028,.034,.037),
(.145,.129,.013,.030,.032,.038),(.23,.125,.015,.038,.039,.054),
(.315,.121,.018,.045,.048,.063),(.385,.118,.013,.041,.047,.052),
(.445,.115,-.001,.037,.048,.043),(.48,.114,-.001,.039,.050,.044),
(.53,.111,.002,.044,.055,.051),(.61,.105,.009,.055,.066,.068),
(.70,.097,.013,.064,.075,.081),(.755,.092,.013,.068,.077,.098),
(.80,.088,.012,.070,.078,.112),(.841,.086,.012,.071,.077,.115)]
# Single surface from the crotch through abdomen, rib cage and neck.
trunkrows=[(.885,.138,.082,.115),(.93,.131,.077,.096),(.99,.119,.069,.077),
(1.055,.117,.070,.075),(1.12,.132,.076,.075),(1.19,.146,.086,.077),
(1.265,.157,.088,.074),(1.315,.164,.079,.065),(1.345,.146,.062,.055),
(1.365,.097,.043,.043),(1.38,.040,.033,.034),(1.405,.033,.030,.031),(1.455,.032,.030,.032)]
exec(compile((SOURCE/'surface.py').read_text(),str(SOURCE/'surface.py'),'exec'))
# Continuous arms and articulated hands. Palm width is about 72 mm; fingers have phalanges,
# broad roots, tapered pads and a short distal segment rather than a single thin tube.
for side in [-1,1]:
    pts=[(side*.108,.007,1.313),(side*.174,.006,1.302),(side*.202,.005,1.266),
         (side*.237,.001,1.199),(side*.270,-.002,1.133),(side*.282,-.003,1.106),
         (side*.313,-.008,1.038),(side*.339,-.011,.968),(side*.364,-.013,.894),(side*.369,-.013,.878)]
    body_parts.append(tube('Body arm',pts,[.051,.044,.043,.037,.029,.030,.033,.028,.020,.020],skin,n=20,sub=2,depth=.90))
    # Local hand frame: U across knuckles, V wrist toward middle fingertip, D dorsal.
    wrist=Vector((side*.364,-.013,.898));u=Vector((side*.97,0,.243));vv=Vector((side*.243,0,-.97));d=Vector((0,1,0))
    def handpoint(x,t,y=0):return wrist+u*x+vv*t+d*y
    verts=[];faces=[];nn=16
    for t,w,dep,shift in [(0,.019,.017,0),(.017,.025,.019,0),(.040,.034,.018,0),(.067,.035,.016,.002),(.080,.030,.014,.003)]:
        for j in range(nn):
            a=2*pi*j/nn
            # Rounded rectangular palm, wider than the metacarpals and flatter on the back.
            x=w*math.copysign(abs(cos(a))**.65,cos(a))+shift
            y=dep*math.copysign(abs(sin(a))**.65,sin(a))
            verts.append(handpoint(x,t,y))
    for k in range(4):
        for j in range(nn):a=k*nn+j;b=k*nn+(j+1)%nn;faces.append((a,b,b+nn,a+nn))
    faces.extend([tuple(reversed(range(nn))),tuple(4*nn+j for j in range(nn))])
    body_parts.append(mesh('Body palm',verts,faces,skin,sub=2))
    for j,(x,start,length,radius) in enumerate([(-.027,.066,.060,.0078),(-.007,.074,.067,.0082),(.014,.073,.063,.0078),(.033,.067,.048,.0068)]):
        # Tiny fan at the roots; distal joints flex toward the palm, not sideways.
        pts=[handpoint(x,start,.001),handpoint(x,start+.012,.001),
             handpoint(x+(j-1.5)*.0007,start+length*.48,-.0015),
             handpoint(x+(j-1.5)*.001,start+length*.76,-.005),
             handpoint(x+(j-1.5)*.001,start+length-.004,-.009),
             handpoint(x+(j-1.5)*.001,start+length,-.0085)]
        body_parts.append(tube('Body finger',pts,[radius,radius,radius*.90,radius*.79,radius*.68,radius*.40],skin,n=12,sub=2,depth=.83))
    thumb=[handpoint(-.018,.021,-.003),handpoint(-.033,.036,-.007),handpoint(-.044,.054,-.012),handpoint(-.047,.074,-.017),handpoint(-.046,.084,-.016)]
    body_parts.append(tube('Body thumb',thumb,[.016,.014,.011,.009,.0045],skin,n=16,sub=2,depth=.86))
    # Foot extends from heel through arch and metatarsal pad to individually grouped toes.
    x=side*.132
    body_parts.append(sections('Body foot',[(.010,x,-.032,.039,.130,.063),(.018,x,-.032,.043,.138,.065),
        (.036,x,-.027,.043,.133,.063),(.058,x,-.012,.036,.080,.050),(.087,x,.006,.029,.041,.040),(.105,x,.01,.026,.031,.032)],skin,n=32,sub=2))
body=fused(body_parts,'Body_A_complete',.0012)
# Local surface relaxation across welded shoulders, wrist roots and groin.
group=body.vertex_groups.new(name='Join relaxation')
for vert in body.data.vertices:
    x,y,z=vert.co
    shoulder=exp(-((abs(x)-.150)/.055)**2-((z-1.303)/.058)**2)
    pelvis=exp(-(x/.13)**2-((z-.845)/.070)**2)
    wrist=exp(-((abs(x)-.363)/.032)**2-((z-.887)/.032)**2)
    ankle=exp(-((abs(x)-.13)/.045)**2-((z-.080)/.045)**2)
    weight=max(shoulder,pelvis,wrist,ankle)
    if weight>.015:group.add([vert.index],weight,'REPLACE')
sm=body.modifiers.new('Blend anatomical junctions','SMOOTH');sm.factor=1.1;sm.iterations=45;sm.vertex_group=group.name
bpy.context.view_layer.objects.active=body;bpy.ops.object.modifier_apply(modifier=sm.name)
# Bare feet and the dressed soles share the same ground plane.
foot_min=min(v.co.z for v in body.data.vertices)
for vert in body.data.vertices:
    if vert.co.z<.060:vert.co.z=(vert.co.z-foot_min)*.060/(.060-foot_min)
body['purpose']='Complete skin surface: torso, pelvis, glutes, legs, feet, arms and joined hands; never delete under clothing.'
# Head surface, with a nose bridge, nasal tip, cheeks and lips displaced from the face itself.
# Head remains separate from body so future avatar head/hair swaps remain possible.
headrows=[(1.432,-.013,.014,.041,.030),(1.441,-.006,.030,.050,.039),
(1.46,.003,.055,.062,.051),(1.485,.005,.071,.067,.065),(1.512,.005,.079,.070,.076),
(1.54,.006,.079,.069,.080),(1.568,.009,.077,.067,.081),(1.596,.011,.074,.062,.077),
(1.622,.012,.061,.052,.061),(1.639,.011,.034,.029,.034),(1.643,.01,.003,.003,.003)]
def interp(z,col):
    for i in range(len(headrows)-1):
        if headrows[i][0]<=z<=headrows[i+1][0]:
            t=(z-headrows[i][0])/(headrows[i+1][0]-headrows[i][0]);return headrows[i][col]*(1-t)+headrows[i+1][col]*t
    return headrows[-1][col]
def face_y(x,z):
    cy,w,df=interp(z,1),interp(z,2),interp(z,3)
    c=sqrt(max(0,1-(x/w)**2))
    yy=cy-df*c**.42
    yy-=.005*exp(-(x/.007)**2)*exp(-((z-1.523)/.019)**2) # bridge
    yy-=.015*exp(-(x/.0065)**2)*exp(-((z-1.500)/.0075)**2) # tip
    yy-=.003*exp(-((abs(x)-.0075)/.0035)**2)*exp(-((z-1.495)/.004)**2) # alae
    yy-=.0020*exp(-(x/.014)**4)*exp(-((z-1.472)/.004)**2) # lips
    yy+=.0015*exp(-((abs(x)-.037)/.019)**2)*exp(-((z-1.525)/.013)**2) # sockets
    return yy
v=[];f=[];nr=86;nn=96
for k in range(nr):
    z=1.432+(1.643-1.432)*k/(nr-1);cy,w,df,db=[interp(z,c) for c in range(1,5)]
    for j in range(nn):
        a=2*pi*j/nn;x=w*sin(a);c=cos(a);y=face_y(x,z) if c>=0 else cy-db*c
        v.append((x,y,z))
for k in range(nr-1):
    for j in range(nn):a=k*nn+j;b=k*nn+(j+1)%nn;f.append((a,b,b+nn,a+nn))
f.extend([tuple(reversed(range(nn))),tuple((nr-1)*nn+j for j in range(nn))])
head=mesh('Head sculpt',v,f,skin,sub=1);heads=[head]
# Recessed pinna, broad attached root and rolled outer helix. Ear interior is part of its surface.
for side in [-1,1]:
    verts=[];faces=[];nn=32
    for k,(radius,depth) in enumerate([(1,0),(.91,-.0035),(.74,-.004),(.55,.001),(.23,.003),(.01,.003)]):
        for j in range(nn):
            a=2*pi*j/nn
            xx=.079+(.0115*cos(a)+.0025*sin(a))*radius
            zz=1.503+.025*sin(a)*radius
            yy=.001+depth+.005*cos(a)
            verts.append((side*xx,yy,zz))
    for k in range(5):
        for j in range(nn):a=k*nn+j;b=k*nn+(j+1)%nn;faces.append((a,b,b+nn,a+nn))
    # Back closes at the head-side root; the inner half embeds in the skull.
    verts.append((side*.074,.015,1.501));back=len(verts)-1
    for j in range(nn):faces.append((back,(j+1)%nn,j))
    faces.append(tuple(5*nn+j for j in range(nn)))
    heads.append(mesh('Pinna sculpt',verts,faces,skin,sub=1))
head=fused(heads,'Head_A_integrated',.00075)
# Features sit on the actual sculpt surface; no protruding white eye disks or nose overlay.
for side in [-1,1]:
    outline=[(.016,1.524),(.027,1.531),(.044,1.532),(.061,1.528),(.052,1.520),(.039,1.517),(.025,1.519)]
    def eye_p(x,z,off=.0010):return(side*x,face_y(x,z)-off,z)
    verts=[eye_p(.038,1.524,.0018)]+[eye_p(x,z) for x,z in outline]
    mesh('Eye sclera',verts,[(0,i+1,(i+1)%len(outline)+1) for i in range(len(outline))],white)
    # Iris conforms to the head rather than projecting through a planar sclera.
    for label,rx,rz,material,off in [('Iris',.0063,.0073,iris,.0021),('Pupil',.0028,.0054,ink,.0024)]:
        vv=[eye_p(.038,1.524,off)]
        for j in range(32):
            a=2*pi*j/32;vv.append(eye_p(.038+rx*cos(a),1.524+rz*sin(a),off))
        mesh(label,vv,[(0,j+1,(j+1)%32+1) for j in range(32)],material)
    ellipsoid('Eye glint',eye_p(.036,1.528,.0029),(.0011,.0005,.0013),white,12,8)
    line('Upper lash',[eye_p(x,z,.0016) for x,z in outline[:4]],.0008,ink)
    line('Lower lid',[eye_p(x,z,.0011) for x,z in outline[3:]+[outline[0]]],.00065,skin)
    brow=[(.018,1.543),(.038,1.549),(.061,1.543),(.058,1.545),(.038,1.552),(.019,1.546)]
    mesh('Eyebrow', [eye_p(x,z,.0011) for x,z in brow], [tuple(range(6))],hair)
line('Mouth',[(x,face_y(x,1.470)-.0005,1.470+.0006*abs(x)/.016) for x in [-.016,-.008,0,.008,.016]],.00055,ink)
# Fitted undershirt, retaining the complete skin underneath.
shirtrows=[(.921,0,.008,.146,.094,.116),(.942,0,.008,.143,.092,.111),(.99,0,.008,.133,.081,.091),
(1.06,0,.008,.132,.084,.088),(1.13,0,.008,.143,.089,.086),(1.20,0,.008,.156,.099,.088),
(1.29,0,.008,.165,.094,.082),(1.34,0,.008,.140,.068,.065),(1.358,0,.008,.060,.048,.044)]
shirtob=sections('Outfit undershirt',shirtrows,shirt,n=32,sub=2)
# Remove the top cap so hiding the jacket reveals an actual neckline.
me=shirtob.data;bm=bmesh.new();bm.from_mesh(me)
cap=max(bm.faces,key=lambda ff:len(ff.verts) if sum(v.co.z for v in ff.verts)/len(ff.verts)>1.35 else 0)
bmesh.ops.delete(bm,geom=[cap],context='FACES');bm.to_mesh(me);bm.free()
for vv in me.vertices:
    if vv.co.z>1.35:vv.co.z-=.023*max(0,min(1,(.008-vv.co.y)/.048))
# Fit the previous jacket to the more substantial chest/back without changing the approved hair.
for ob in model.objects:
    if ob.name.startswith(('Cobalt jacket','Ivory front piping','Ivory jacket hem','Back centre seam','Jacket pocket welt')):
        coords=[vv.co for vv in ob.data.vertices] if ob.type=='MESH' else [bp.co for sp in ob.data.splines for bp in sp.bezier_points]
        for co in coords:
            if co.z<1.34:
                amount=.017*max(0,min(1,(1.36-co.z)/.20))
                co.y += amount if co.y>.008 else -amount
# Shoes follow the new straight-leg rest stance; foot size was already adequate.
for ob in model.objects:
    if ob.name.startswith(('Rubber outsole','Sneaker upper','Ivory toe cap','Lace','Ivory shoe quarter')):
        coords=[vv.co for vv in ob.data.vertices] if ob.type=='MESH' else [bp.co for sp in ob.data.splines for bp in sp.bezier_points]
        for co in coords:
            co.x-=.075 if co.x>0 else -.075
            co.z-=.006
# Explicit collections are the outfit-switch contract of this source file.
body_collection=bpy.data.collections.new('01_BODY_complete');model.children.link(body_collection)
hair_collection=bpy.data.collections.new('02_HAIR_removable');model.children.link(hair_collection)
outfit_collection=bpy.data.collections.new('03_OUTFIT_removable');model.children.link(outfit_collection)
for ob in list(model.objects):
    if ob.name.startswith(('Hair cap','Fringe','Side and nape','Crown')):target=hair_collection
    elif ob.name.startswith(('Body_A','Head_A','Eye','Iris','Pupil','Upper lash','Lower lid','Mouth')):target=body_collection
    else:target=outfit_collection
    relocate(ob,target)
# Subsequent build stages iterate all_objects to include these named collections.
