"""Character A sculpted blockout, #99. Blender 5.2.2, no external assets.
Run: blender -b --factory-startup --python assets/source/benchmark-character99/build.py
Hand-authored surface sections and hair locks; no generated/existing mesh input.
"""
import bpy, math, json, time
from pathlib import Path
from mathutils import Vector
from math import sin, cos, pi
ROOT = Path(__file__).resolve().parents[3]
SOURCE = Path(__file__).resolve().parent
EVIDENCE = ROOT / 'docs/requests/evidence/character99'
EXPORT = ROOT / 'assets/characters/benchmark/character_a_base.glb'
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.materials, bpy.data.curves, bpy.data.meshes):
    for item in list(datablocks):
        if item.users == 0: datablocks.remove(item)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
model = bpy.data.collections.new('CHARACTER_A_BASE'); scene.collection.children.link(model)
review = bpy.data.collections.new('REVIEW_ONLY'); scene.collection.children.link(review)
def relocate(obj, collection=model):
    for c in list(obj.users_collection): c.objects.unlink(obj)
    collection.objects.link(obj)
    return obj
def mat(name, rgb, rough=0.85):
    m=bpy.data.materials.new(name); m.diffuse_color=(*rgb,1); m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*rgb,1); bs.inputs['Roughness'].default_value=rough
    return m
skin=mat('Skin • warm ochre',(.63,.39,.22)); skinlight=mat('Skin • lid',(.69,.44,.26))
hair=mat('Hair • espresso',(.054,.039,.030)); hairlight=mat('Hair • brown planes',(.077,.052,.037))
blue=mat('Jacket • cobalt',(.042,.092,.235)); blueedge=mat('Jacket • cuff',(.032,.063,.16))
ivory=mat('Piping • warm ivory',(.83,.79,.65)); shirt=mat('Shirt • charcoal',(.043,.046,.049))
pants=mat('Trousers • charcoal',(.038,.041,.046)); sole=mat('Sole • warm rubber',(.72,.68,.57))
shoe=mat('Shoes • ink',(.025,.031,.038)); white=mat('Eyes • warm white',(.86,.84,.75))
ink=mat('Features • umber',(.025,.015,.010)); iris=mat('Iris • brown',(.19,.09,.029))
def mesh(name,verts,faces,material,smooth=True,sub=0):
    me=bpy.data.meshes.new(name); me.from_pydata(verts,[],faces); me.update()
    ob=bpy.data.objects.new(name,me); model.objects.link(ob); ob.data.materials.append(material)
    for p in me.polygons: p.use_smooth=smooth
    if sub:
        mo=ob.modifiers.new('Surface refinement','SUBSURF'); mo.levels=sub; mo.render_levels=sub
    return ob
# Horizontal anatomical sections: z, centre x, centre y, half width, front depth, rear depth.
def sections(name, rows, material, n=24, sub=1):
    v=[]
    for z,x,y,w,df,db in rows:
        for j in range(n):
            a=2*pi*j/n; c=cos(a)

            front = c**.38 if name == 'Head' and c >= 0 else c
            v.append((x+w*sin(a),y-front*(df if c>=0 else db),z))
    f=[]
    for k in range(len(rows)-1):
        for j in range(n):
            a=k*n+j; b=k*n+(j+1)%n; f.append((a,b,b+n,a+n))
    f.extend([tuple(reversed(range(n))),tuple((len(rows)-1)*n+j for j in range(n))])
    return mesh(name,v,f,material,sub=sub)
def tube(name, points, radii, material, n=10, sub=1, depth=1):
    pts=[Vector(p) for p in points]; v=[]
    for i,p in enumerate(pts):
        tangent=pts[min(i+1,len(pts)-1)]-pts[max(0,i-1)]; tangent.normalize()
        axis=Vector((0,1,0)); u=tangent.cross(axis).normalized(); w=tangent.cross(u).normalized()
        for j in range(n):
            a=j*2*pi/n; v.append(p+radii[i]*(u*cos(a)+w*sin(a)*depth))
    f=[]
    for i in range(len(pts)-1):
        for j in range(n):
            a=i*n+j; b=i*n+(j+1)%n; f.append((a,b,b+n,a+n))
    f.extend([tuple(reversed(range(n))),tuple((len(pts)-1)*n+j for j in range(n))])
    return mesh(name,v,f,material,sub=sub)
def line(name,pts,r,material):
    cu=bpy.data.curves.new(name,'CURVE'); cu.dimensions='3D'; cu.resolution_u=16
    sp=cu.splines.new('BEZIER'); sp.bezier_points.add(len(pts)-1)
    for b,p in zip(sp.bezier_points,pts): b.co=p; b.handle_left_type='AUTO'; b.handle_right_type='AUTO'
    cu.bevel_depth=r; cu.bevel_resolution=2
    ob=bpy.data.objects.new(name,cu); model.objects.link(ob); ob.data.materials.append(material); return ob
def ellipsoid(name,location,scale,material,segments=24,rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,location=location)
    ob=relocate(bpy.context.object); ob.name=name; ob.scale=scale; ob.data.materials.append(material)
    for p in ob.data.polygons:p.use_smooth=True
    return ob
# Head: tapered jaw and broad upper face; features remain separate in the blockout.
sections('Head',[
(1.433,0,-.025,.009,.034,.030),(1.444,0,-.013,.027,.048,.039),
(1.465,0,0,.057,.063,.052),(1.491,0,.003,.074,.070,.065),
(1.514,0,.004,.080,.073,.075),(1.539,0,.005,.079,.072,.080),
(1.564,0,.008,.077,.069,.082),(1.591,0,.010,.075,.065,.080),
(1.616,0,.012,.064,.055,.069),(1.636,0,.011,.042,.038,.047),
(1.642,0,.010,.009,.010,.011)],skin,n=32,sub=2)
sections('Neck',[(1.337,0,.009,.042,.037,.039),(1.35,0,.009,.042,.037,.039),(1.423,0,.012,.033,.032,.034),(1.462,0,.01,.032,.031,.032)],skin,n=20,sub=1)
for s in [-1,1]:
    ear=ellipsoid(('L' if s>0 else 'R')+' ear',(s*.079,.006,1.498),(.012,.010,.025),skin)
    line('Ear inner fold',[(s*.085,-.005,1.518),(s*.091,-.007,1.506),(s*.085,-.007,1.492)],.0026,skinlight)
# Nose bridge and tip integrated-looking patch, not a round sphere.
mesh('Nose bridge',[(0,-.066,1.544),(-.009,-.069,1.517),(.009,-.069,1.517),
(-.011,-.074,1.496),(.011,-.074,1.496),(0,-.094,1.500),(0,-.082,1.49)],
[(0,1,5),(0,5,2),(1,3,5),(2,5,4),(3,6,5),(4,5,6)],skin,sub=1)
line('Mouth', [(-.017,-.064,1.472),(-.006,-.066,1.471),(0,-.067,1.471),(.016,-.064,1.472)],.00095,ink)
line('Lower lip', [(-.006,-.064,1.466),(0,-.065,1.465),(.008,-.064,1.466)],.001,skinlight)
# Almond-shaped eye surfaces follow the face; pupils and lids use shallow curved patches.
for s in [-1,1]:
    def ep(x,y,z):return(s*x,y,z)
    outline=[(.016,-.070,1.524),(.026,-.0715,1.530),(.044,-.070,1.532),(.061,-.060,1.529),(.055,-.064,1.520),(.039,-.071,1.516),(.023,-.071,1.518)]
    verts=[ep(.038,-.073,1.524)]+[ep(*p) for p in outline]
    mesh('Eye white',verts,[(0,i+1,(i+1)%len(outline)+1) for i in range(len(outline))],white)
    ellipsoid('Iris',(s*.039,-.073,1.524),(.0072,.0012,.0085),iris)
    ellipsoid('Pupil',(s*.039,-.0742,1.525),(.0032,.0005,.0058),ink)
    ellipsoid('Eye glint',(s*.036,-.0748,1.529),(.0018,.0007,.0021),white,12,8)
    line('Upper eyelid',[ep(*p) for p in outline[:4]],.002,ink)
    line('Lower eyelid',[ep(*p) for p in outline[3:]+[outline[0]]],.00085,skinlight)
    line('Eyebrow',[ep(.018,-.070,1.544),ep(.039,-.070,1.550),ep(.062,-.053,1.544)],.0026,hair)
# Torso underlayer, chest/waist/hem contours.
sections('Charcoal shirt',[(.900,0,.008,.132,.075,.078),(.914,0,.008,.134,.078,.079),
(.958,0,.007,.129,.075,.078),(1.03,0,.008,.121,.072,.079),(1.10,0,.006,.125,.074,.078),
(1.19,0,.007,.147,.079,.082),(1.29,0,.009,.168,.082,.078),(1.343,0,.009,.148,.066,.067),
(1.357,0,.009,.082,.051,.049),(1.353,0,.009,.046,.040,.038)],shirt,n=32,sub=1)
line('Shirt neckline',[(-.05,-.03,1.356),(-.037,-.045,1.334),(0,-.052,1.326),(.037,-.045,1.334),(.05,-.03,1.356)],.003,shirt)
# Open jacket wraps continuously around back; front panels have a deliberate angular opening.
jrows=[(.995,.157,.088,.084,.070),(1.004,.158,.089,.085,.071),(1.07,.136,.084,.081,.065),
(1.16,.141,.086,.083,.057),(1.25,.166,.091,.086,.049),(1.333,.177,.087,.077,.042),
(1.35,.166,.080,.071,.041),(1.372,.061,.047,.048,.039),(1.407,.048,.041,.041,.037),(1.416,.048,.041,.041,.037)]
v=[]; nc=33
for z,w,df,db,opening in jrows:
    start=math.asin(min(opening/w,.98))
    for j in range(nc):
        a=start+(2*pi-2*start)*j/(nc-1); c=cos(a)
        v.append((w*sin(a),.008-c*(df if c>=0 else db),z))
f=[]
for k in range(len(jrows)-1):
    for j in range(nc-1):a=k*nc+j; f.append((a,a+1,a+1+nc,a+nc))
jacket=mesh('Cobalt jacket • open shell',v,f,blue,sub=1)
sol=jacket.modifiers.new('Cloth thickness','SOLIDIFY'); sol.thickness=.003
for side in [0,nc-1]:
    line('Ivory front piping',[v[k*nc+side] for k in range(len(jrows))],.0032,ivory)
line('Ivory jacket hem',v[:nc],.0032,ivory)
line('Ivory collar rim',v[-nc:],.0025,ivory)
line('Back centre seam',[(0,.095,1.009),(0,.092,1.18),(0,.086,1.30),(0,.058,1.37)],.0009,blueedge)
for s in [-1,1]:
    # Sleeves swing down 21 degrees from vertical, as in the front sheet.
    pts=[(s*.127,.008,1.301),(s*.171,.009,1.306),(s*.216,.01,1.271),(s*.245,.004,1.203),
         (s*.269,-.008,1.146),(s*.278,-.015,1.129),(s*.295,-.009,1.107),
         (s*.316,-.009,1.039),(s*.346,-.013,.967),(s*.359,-.016,.927),(s*.359,-.016,.923)]
    rad=[.067,.067,.060,.052,.048,.052,.046,.043,.040,.036,.036]
    tube('Jacket sleeve',pts,rad,blue,n=16,sub=1,depth=.89)
    cuffpts=[(s*.354,-.016,.935),(s*.359,-.016,.923),(s*.361,-.016,.918)]
    tube('Ivory cuff',cuffpts,[.0365,.0365,.036],ivory,n=16,sub=1,depth=.89)
    # Soft fold ridges use cobalt, not drawn black lines.
    line('Elbow fold',[(s*.249,-.041,1.155),(s*.274,-.058,1.139),(s*.293,-.041,1.131)],.0025,blueedge)
    line('Jacket pocket welt',[(s*.090,-.060,1.08),(s*.128,-.039,1.091)],.002,blueedge)
    # Palm with relaxed finger fan. Separate articulated volumes are intentional at blockout stage.
    sections('Hand palm',[(.830,s*.386,-.018,.023,.013,.016),(.846,s*.382,-.018,.026,.016,.018),
        (.883,s*.374,-.018,.025,.018,.019),(.908,s*.363,-.017,.017,.017,.018),(.93,s*.358,-.016,.016,.017,.017)],skin,n=16,sub=1)
    for j,(offset,length) in enumerate([(-.017,.057),(-.005,.067),(.007,.063),(.018,.050)]):
        x=s*(.387+offset)
        tube('Finger '+str(j+1),[(x,-.019,.847),(x+s*.003,-.024,.828),
             (x+s*.006,-.033,.847-length*.70),(x+s*.003,-.037,.847-length),(x+s*.003,-.036,.844-length)],
             [.0068,.0067,.0057,.0046,.0018],skin,n=8,sub=1)
    tube('Thumb',[(s*.358,-.021,.883),(s*.346,-.032,.862),(s*.345,-.039,.840),(s*.350,-.043,.830)], [.010,.010,.007,.003],skin,n=10,sub=1)
# Continuous trouser topology: two leg tubes joined through a crotch saddle to one waist.
v=[]; f=[]; n=20; tops=[]
for side in [1,-1]:
    rows=[(.085,.207,.010,.048,.051,.048),(.105,.206,.012,.052,.055,.050),
          (.145,.200,.010,.048,.052,.051),(.25,.18,.006,.046,.052,.054),
          (.36,.148,-.006,.048,.055,.057),(.423,.132,-.019,.054,.055,.054),
          (.450,.128,-.012,.051,.056,.055),(.48,.125,-.006,.054,.058,.058),
          (.58,.11,.008,.060,.068,.071),(.69,.096,.010,.065,.072,.077),
          (.81,.079,.011,.075,.075,.080)]
    off=len(v)
    for k,(z,x,y,w,df,db) in enumerate(rows):
        for j in range(n):
            a=j*2*pi/n; ss=sin(a); cc=cos(a)
            zz=z
            if k==len(rows)-1: zz+=.03*max(0,side*ss)-.020*max(0,-side*ss)+.052*abs(cc)
            v.append((side*x+w*ss,y-cc*(df if cc>=0 else db),zz))
    for k in range(len(rows)-1):
        for j in range(n):
            a=off+k*n+j;b=off+k*n+(j+1)%n;f.append((a,b,b+n,a+n))
    f.append(tuple(reversed([off+j for j in range(n)])))
    tops.append([off+(len(rows)-1)*n+j for j in range(n)])
r,l=tops
# The inner half of each top leg ring is the underside of the crotch.
for j in range(10):
    f.append((r[(11+j)%n],l[(9-j)%n],l[(10-j)%n],r[(10+j)%n]))
perimeter=r[:11]+l[10:]+[l[0]]
# Angle-following upper rings preserve the flat front and the seat.
prev=perimeter
for z,width,df,db in [(.895,.140,.081,.089),(.918,.133,.078,.084),(.928,.132,.077,.083)]:
    nxt=[]
    for idx in perimeter:
        x,y,_=v[idx]; a=math.atan2(x,(.011-y)*1.8); c=cos(a)
        nxt.append(len(v));v.append((width*sin(a),.01-c*(df if c>=0 else db),z))
    for j in range(len(prev)):f.append((prev[j],prev[(j+1)%len(prev)],nxt[(j+1)%len(prev)],nxt[j]))
    prev=nxt
f.append(tuple(prev))
mesh('Trousers • continuous base',v,f,pants,sub=2)
for side in [-1,1]:
    line('Front pocket',[(side*.119,-.032,.906),(side*.111,-.051,.878),(side*.084,-.066,.855)],.0008,shirt)
# Shoes use flattened oval perimeter sections in z, longer toward the toes.
for s in [-1,1]:
    x=s*.207
    sections('Rubber outsole',[(.006,x,-.025,.054,.111,.064),(.01,x,-.025,.059,.116,.067),
(.023,x,-.025,.059,.116,.067),(.03,x,-.025,.057,.112,.065)],sole,n=32,sub=1)
    sections('Sneaker upper',[(.025,x,-.022,.055,.111,.063),(.037,x,-.022,.055,.11,.061),
(.055,x,-.017,.051,.099,.055),(.076,x,-.005,.045,.068,.050),(.092,x,.009,.042,.040,.043),(.101,x,.011,.037,.036,.040)],shoe,n=32,sub=1)
    # Toe cap is a custom curved patch, not an extra primitive overlapping the toe.
    verts=[]
    for yy,ww,zz in [(-.128,.030,.043),(-.116,.044,.060),(-.094,.045,.069),(-.081,.042,.071)]:
        for j in range(9):
            a=-pi/2+pi*j/8; verts.append((x+ww*sin(a),yy,zz-.017*(sin(a)**2)))
    mesh('Ivory toe cap',verts,[(k*9+j,k*9+j+1,(k+1)*9+j+1,(k+1)*9+j) for k in range(3) for j in range(8)],ivory,sub=1)
    for i in range(5):
        yy=-.069+i*.012; zz=.078+i*.004
        line('Lace',[(x-.020,yy,zz),(x,yy-.003,zz+.004),(x+.020,yy+.003,zz)],.0017,ivory)
    for side in [-1,1]:
        mesh('Ivory shoe quarter',[(x+side*.046,-.027,.032),(x+side*.046,.033,.032),(x+side*.037,.043,.078),(x+side*.042,.005,.089)],[(0,1,2,3)],ivory)
# Hair cap: irregular lower hairline, crown forms are hidden beneath swept locks.
v=[]; n=40
for k in range(9):
    t=k/8
    for j in range(n):
        a=2*pi*j/n
        bottom=1.595-.14*(1-cos(a))/2+.004*sin(7*a)
        z=bottom+(1.657-bottom)*sin(t*pi/2)
        r=cos(t*pi/2)
        v.append((.088*sin(a)*r,.012-.088*cos(a)*r,z))
f=[(k*n+j,k*n+(j+1)%n,(k+1)*n+(j+1)%n,(k+1)*n+j) for k in range(8) for j in range(n)]
mesh('Hair cap',v,f,hair,sub=1)
# Broad tapered locks, with a raised central ridge and a shallow underside.
def lock(name,points,widths,thickness,material):
    pts=[Vector(p) for p in points]; verts=[]
    for i,p in enumerate(pts):
        tan=(pts[min(i+1,len(pts)-1)]-pts[max(0,i-1)]).normalized()
        outward=Vector((p.x,(p.y-.01),max(.012,p.z-1.56))).normalized()
        across=tan.cross(outward).normalized(); normal=across.cross(tan).normalized()
        if normal.dot(outward)<0:normal=-normal
        w=widths[i]
        for dx,dz in [(-1,0),(-.52,.55),(0,1),(.52,.55),(1,0),(0,-.25)]:
            verts.append(p+across*w*dx+normal*thickness*.65*(w/max(widths))*dz)
    faces=[]
    for k in range(len(pts)-1):
        for j in range(6):faces.append((k*6+j,k*6+(j+1)%6,(k+1)*6+(j+1)%6,(k+1)*6+j))
    faces.extend([tuple(reversed(range(6))),tuple((len(pts)-1)*6+j for j in range(6))])
    mesh(name,verts,faces,material,sub=1)
# Fringe deliberately asymmetric; ends frame eyebrows and bridge without obscuring both eyes.
locks=[
([(.028,-.034,1.656),(.012,-.075,1.623),(-.004,-.090,1.581),(-.019,-.083,1.535)],[.014,.027,.020,.0005]),
([(.045,-.025,1.65),(.047,-.068,1.613),(.034,-.088,1.581),(.021,-.086,1.550)],[.014,.026,.019,.0005]),
([(-.008,-.043,1.65),(-.031,-.074,1.614),(-.041,-.084,1.577),(-.052,-.073,1.541)],[.014,.025,.018,.0005]),
([(-.026,-.034,1.644),(-.061,-.059,1.61),(-.073,-.061,1.566),(-.076,-.05,1.527)],[.014,.024,.018,.0005]),
([(.056,-.018,1.636),(.077,-.049,1.602),(.076,-.06,1.563),(.064,-.068,1.542)],[.015,.024,.017,.0005]),
([(-.027,-.01,1.656),(-.065,-.035,1.633),(-.091,-.04,1.607),(-.112,-.033,1.590)],[.010,.024,.015,.0005]),
([(.024,.011,1.653),(.064,-.01,1.639),(.092,-.025,1.612),(.107,-.027,1.594)],[.013,.024,.017,.0005]),
]
for i,(p,w) in enumerate(locks):lock('Fringe %02d'%i,p,w,.009,hairlight if i in [1,5] else hair)
# Side / rear layered pointed tufts, swept away from crown with varied tips.
for i in range(17):
    a=.82+i*(2*pi-1.64)/16
    sweep=.17*sin(i*1.7)+.12
    ztip=1.495+.025*sin(i*2.1)-.05*max(0,-cos(a))
    p=[(.025*sin(a),.014-.025*cos(a),1.65),
       (.078*sin(a+.12),.014-.078*cos(a+.12),1.615+.01*sin(i)),
       (.093*sin(a+sweep),.014-.093*cos(a+sweep),1.565+.015*sin(i*1.6)-.03*max(0,-cos(a))),
       (.102*sin(a+sweep+.15),.014-.102*cos(a+sweep+.15),ztip)]
    lock('Side and nape %02d'%i,p,[.012,.027,.023,.0005],.009,hairlight if i%5==0 else hair)
# Additional outward crown breaks keep the cap from reading as a bowl.
for i in range(9):
    a=i*2*pi/9+.2
    p=[(.016*sin(a),.016-.016*cos(a),1.657),
       (.053*sin(a+.22),.016-.053*cos(a+.22),1.662),
       (.078*sin(a+.3),.016-.078*cos(a+.3),1.640),
       (.101*sin(a+.43),.016-.101*cos(a+.43),1.613+.015*sin(i*3))]
    lock('Crown sweep %02d'%i,p,[.018,.031,.024,.0005],.009,hair)
for i,(p,w) in enumerate([
([(-.026,.025,1.646),(-.021,.012,1.674),(-.050,.008,1.697)],[.025,.019,.0004]),
([(.0,.022,1.65),(.016,.029,1.676),(.046,.04,1.682)],[.021,.015,.0004]),
([(-.013,.033,1.648),(-.052,.039,1.666),(-.084,.049,1.67)],[.022,.015,.0004]),
([(.028,.04,1.638),(.074,.055,1.655),(.106,.056,1.641)],[.022,.015,.0004])]):lock('Crown %02d'%i,p,w,.009,hair)
# Lower the shirt neckline into a scoop instead of a straight band across the throat.
ob=bpy.data.objects['Charcoal shirt']
for vv in ob.data.vertices:
    if vv.co.z>1.349:
        front=max(0, min(1, (.009-vv.co.y)/.04))
        vv.co.z-=.024*front
# Match wrist and fingertip heights to the front reference while retaining the A-pose.
for ob in model.objects:
    if any(ob.name.startswith(n) for n in ['Jacket sleeve','Ivory cuff','Elbow fold','Hand palm','Finger','Thumb']):
        coords = [vv.co for vv in ob.data.vertices] if ob.type=='MESH' else [bp.co for sp in ob.data.splines for bp in sp.bezier_points]
        for co in coords:
            factor=max(0,min(1,(1.30-co.z)/.38))
            co.z-=.055*factor
            co.x+= (.012 if co.x>0 else -.012)*factor
            if ob.name.startswith('Finger'):co.z-=max(0,.842-(co.z+.055))*.25
# Final silhouette corrections from the reference/render comparison.
for ob in model.objects:
    if ob.name.startswith(('Hair cap','Fringe','Side and nape','Crown')):
        for vv in ob.data.vertices: vv.co.x*=1.18; vv.co.y*=1.14
    if ob.name.startswith(('Jacket sleeve','Ivory cuff','Elbow fold')):
        coords=[vv.co for vv in ob.data.vertices] if ob.type=='MESH' else [bp.co for sp in ob.data.splines for bp in sp.bezier_points]
        for co in coords:
            f=max(0,min(1,(co.z-1.10)/.2)); co.x*=1-.08*f
    if ob.name.startswith(('Rubber outsole','Sneaker upper','Ivory toe cap','Lace','Ivory shoe quarter')):
        coords=[vv.co for vv in ob.data.vertices] if ob.type=='MESH' else [bp.co for sp in ob.data.splines for bp in sp.bezier_points]
        for co in coords:
            center=.207 if co.x>0 else -.207
            co.x=center+(co.x-center)*1.22; co.y=.01+(co.y-.01)*1.40
# Recalculate winding consistently on all authored mesh islands.
import bmesh
for ob in model.objects:
    if ob.type=='MESH':
        bm=bmesh.new(); bm.from_mesh(ob.data); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(ob.data); bm.free()
# Set lowest sole to zero and exact full height to 1.70 m, uniformly across all model parts.
dg=bpy.context.evaluated_depsgraph_get(); zvalues=[]
for ob in model.objects:
    ev=ob.evaluated_get(dg); mm=ev.to_mesh(); zvalues.extend([(ev.matrix_world@vv.co).z for vv in mm.vertices]); ev.to_mesh_clear()
zmin,zmax=min(zvalues),max(zvalues); factor=1.70/(zmax-zmin)
from mathutils import Matrix
normalization=Matrix.Scale(factor,4)@Matrix.Translation((0,0,-zmin))
for ob in model.objects:ob.matrix_world=normalization@ob.matrix_world
# Collect metadata before adding studio objects.
for ob in model.objects:ob['stage']='99: proportion / silhouette base; unrigged'
# Blender -Y maps to Godot +Z; rotate to meet Godot -Z facing contract.
# Authoring front is -Y, so rotate mesh objects 180 degrees for Godot export only below.
scene.world.color=(.3,.3,.3)
world=scene.world; world.use_nodes=True; world.node_tree.nodes['Background'].inputs[0].default_value=(.38,.40,.43,1); world.node_tree.nodes['Background'].inputs[1].default_value=.65
floor_mat=mat('Studio • grey',(.24,.26,.29))
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.002)); floor=relocate(bpy.context.object,review); floor.name='Studio floor'; floor.data.materials.append(floor_mat)
def area(name,loc,power,size):
    bpy.ops.object.light_add(type='AREA',location=loc); ob=relocate(bpy.context.object,review); ob.name=name; ob.data.energy=power; ob.data.shape='DISK'; ob.data.size=size; ob.rotation_euler=(Vector((0,0,.95))-ob.location).to_track_quat('-Z','Y').to_euler()
area('Large key',(-3,-4,6),500,4); area('Fill',(3,-2,3),220,3); area('Rim',(1,3,4),400,3)
bpy.ops.object.camera_add(location=(0,-5,.86)); camera=relocate(bpy.context.object,review); camera.name='Review camera'; scene.camera=camera
camera.data.type='ORTHO'; camera.data.ortho_scale=1.86
scene.render.engine='CYCLES'; scene.cycles.samples=64; scene.cycles.use_denoising=True
scene.render.resolution_x=800; scene.render.resolution_y=1000; scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard'; scene.view_settings.look='Medium High Contrast'; scene.view_settings.exposure=0; scene.view_settings.gamma=1
scene.render.image_settings.file_format='PNG'; scene.render.film_transparent=False

def aim(loc,target=(0,0,.85)):
    camera.location=loc; camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
aim((0,-5,.85))
# Save a clean selectable modelling scene with review setup.
bpy.ops.object.select_all(action='DESELECT')
for ob in model.objects:ob.select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects['Head']
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'character_a_base.blend'))
# Export evaluated meshes only, excluding studio; apply facing change temporarily.
original={ob:ob.matrix_world.copy() for ob in model.objects}
from mathutils import Matrix
rot=Matrix.Rotation(pi,4,'Z')
for ob in model.objects:ob.matrix_world=rot@ob.matrix_world
bpy.ops.export_scene.gltf(filepath=str(EXPORT),export_format='GLB',use_selection=True,export_apply=True,export_animations=False,export_cameras=False,export_lights=False)
for ob,matrix in original.items():ob.matrix_world=matrix
stats={'objects':len(model.objects),'triangles_evaluated':0,'height_m':None,'stage':'unrigged base, approval pending','blender':bpy.app.version_string}
dg=bpy.context.evaluated_depsgraph_get(); coords=[]
for ob in model.objects:
    ev=ob.evaluated_get(dg); me=ev.to_mesh(); me.calc_loop_triangles(); stats['triangles_evaluated']+=len(me.loop_triangles)
    coords.extend([ev.matrix_world@vv.co for vv in me.vertices]); ev.to_mesh_clear()
stats['height_m']=round(max(p.z for p in coords)-min(p.z for p in coords),4)
stats['bounds_m']=[[round(min(p[i] for p in coords),4),round(max(p[i] for p in coords),4)] for i in range(3)]
(SOURCE/'metrics.json').write_text(json.dumps(stats,indent=2)+'\n')
for name,loc in [('front',(0,-5,.85)),('side',(5,0,.85)),('three-quarter',(3,-5,.85)),('back',(0,5,.85))]:
    aim(loc); scene.render.filepath=str(EVIDENCE/(name+'.png')); bpy.ops.render.render(write_still=True)
# Face close-up supplements full-height comparisons.
camera.data.ortho_scale=.34; aim((.30,-2,1.552),(0,0,1.552)); scene.render.resolution_x=1000; scene.render.resolution_y=1000
scene.render.filepath=str(EVIDENCE/'head.png'); bpy.ops.render.render(write_still=True)
print('CHARACTER99_DONE',json.dumps(stats))
