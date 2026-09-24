"""Continuous anatomical torso/leg surface, sampled from authored volume profiles.
NumPy-only marching tetrahedra; no imported mesh or external model dependency.
"""
import numpy as np
step=.0038
axes=[np.arange(-.22,.2201,step,dtype=np.float32),np.arange(-.15,.2001,step,dtype=np.float32),np.arange(.025,1.485,step,dtype=np.float32)]
X,Y,Z=np.meshgrid(*axes,indexing='ij')
def smooth_union(a,b,k):
    h=np.clip(.5+.5*(b-a)/k,0,1)
    return b*(1-h)+a*h-k*h*(1-h)
def ellipsoid_field(center,radius):
    q=[(axis-c)/r for axis,c,r in zip((X,Y,Z),center,radius)]
    k0=np.sqrt(sum(v*v for v in q));k1=np.sqrt(sum((v/r)**2 for v,r in zip(q,radius)))
    return k0*(k0-1)/np.maximum(k1,1e-8)
def along(rows,column):
    # Cubic Hermite through authored profiles, with shared tangents at ring heights.
    zz=axes[2];data=np.array(rows,dtype=np.float32);zs=data[:,0];ys=data[:,column]
    delta=np.diff(ys)/np.diff(zs)
    slopes=np.zeros_like(ys);slopes[0]=delta[0];slopes[-1]=delta[-1]
    for j in range(1,len(ys)-1):
        if delta[j-1]*delta[j]>0:
            h0=zs[j]-zs[j-1];h1=zs[j+1]-zs[j];w1=2*h1+h0;w2=h1+2*h0
            slopes[j]=(w1+w2)/(w1/delta[j-1]+w2/delta[j])
    out=np.interp(zz,zs,ys)
    for i in range(len(zs)-1):
        mask=(zz>=zs[i])&(zz<=zs[i+1]);d=zs[i+1]-zs[i];t=(zz[mask]-zs[i])/d
        out[mask]=(2*t**3-3*t*t+1)*ys[i]+(t**3-2*t*t+t)*d*slopes[i]+(-2*t**3+3*t*t)*ys[i+1]+(t**3-t*t)*d*slopes[i+1]
    return out[None,None,:]
# Trunk narrows at waist and neck; lower cap sits inside the pelvic volume.
full_trunk=[(.755,.027,.025,.035),(.790,.073,.057,.076),(.825,.112,.073,.099),(.855,.130,.080,.111)]+trunkrows
w=along(full_trunk,1);df=along(full_trunk,2);db=along(full_trunk,3)
dep=np.where(Y<.008,df,db)
field=(np.sqrt((X/w)**2+((Y-.008)/dep)**2)-1)*np.minimum(w,dep)
field=np.maximum(field,np.maximum(.752-Z,Z-1.458))
field=smooth_union(field,ellipsoid_field((0,.007,.902),(.134,.086,.118)),.030)
# Front/back depths describe muscles about straight hip-to-ankle axes.
for side in [-1,1]:
    upper_leg=legrows+[(.88,.076,.010,.065,.068,.094),(.91,.069,.008,.050,.055,.072),(.94,.065,.008,.025,.028,.038)]
    center=along(upper_leg,1)*side;cy=along(upper_leg,2);w=along(upper_leg,3)
    df=along(upper_leg,4);db=along(upper_leg,5)
    dep=np.where(Y<cy,df,db)
    leg=(np.sqrt(((X-center)/w)**2+((Y-cy)/dep)**2)-1)*np.minimum(w,dep)
    leg=np.maximum(leg,np.maximum(.052-Z,Z-.949))
    field=smooth_union(field,leg,.024)
    # Broad gluteal masses merge into both pelvis and thigh, with soft transitions.
    field=smooth_union(field,ellipsoid_field((side*.064,.049,.834),(.077,.088,.104)),.042)
# March only cells crossed by the isosurface.
corners=np.array([[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]])
shape=np.array(field.shape)-1
values=[field[c[0]:c[0]+shape[0],c[1]:c[1]+shape[1],c[2]:c[2]+shape[2]] for c in corners]
low=np.minimum.reduce(values);high=np.maximum.reduce(values)
cell=np.argwhere((low<0)&(high>=0))
base=np.stack([axes[i][cell[:,i]] for i in range(3)],axis=1)
cv=np.stack([field[cell[:,0]+c[0],cell[:,1]+c[1],cell[:,2]+c[2]] for c in corners],axis=1)
cp=base[:,None,:]+corners[None,:,:]*step
edges=[(0,1),(1,2),(2,0),(0,3),(1,3),(2,3)]
tri_table=[[],[0,3,2],[0,1,4],[1,4,2,2,4,3],[1,2,5],[0,3,5,0,5,1],[0,2,5,0,5,4],[5,4,3],
           [3,4,5],[4,5,0,5,2,0],[1,5,0,5,3,0],[5,2,1],[3,4,2,2,4,1],[4,1,0],[2,3,0],[]]
triangles=[]
for tet in [(0,5,1,6),(0,1,2,6),(0,2,3,6),(0,3,7,6),(0,7,4,6),(0,4,5,6)]:
    tv=cv[:,tet];tp=cp[:,tet];cases=((tv<0)*np.array([1,2,4,8])).sum(axis=1)
    for case in range(1,15):
        mask=cases==case
        if not np.any(mask):continue
        v=tv[mask];p=tp[mask];ev=[]
        for a,b in edges:
            t=v[:,a]/np.where(abs(v[:,a]-v[:,b])>1e-10,v[:,a]-v[:,b],1e-10)
            ev.append(p[:,a,:]+t[:,None]*(p[:,b,:]-p[:,a,:]))
        ep=np.stack(ev,axis=1)
        triangles.append(ep[:,tri_table[case],:].reshape(-1,3,3))
tris=np.concatenate(triangles)
verts,indices=np.unique(np.round(tris.reshape(-1,3),6),axis=0,return_inverse=True)
faces=indices.reshape(-1,3)
faces=faces[(faces[:,0]!=faces[:,1])&(faces[:,1]!=faces[:,2])&(faces[:,0]!=faces[:,2])]
core=mesh('Anatomical torso and legs',verts.tolist(),faces.tolist(),skin)
normalise_normals(core)
body_parts.append(core)
# A fitted trouser shell uses the same complete lower anatomy, with clearance.
trousers=bpy.data.objects.new('Outfit trousers',core.data.copy());model.objects.link(trousers)
trousers.data.materials.clear();trousers.data.materials.append(pants)
trousers=fused([trousers],'Outfit trousers',.0018)
samples=[(v.co.copy(),v.normal.copy()) for v in trousers.data.vertices]
for vert,(co,no) in zip(trousers.data.vertices,samples):vert.co=co+no*.011
bm=bmesh.new();bm.from_mesh(trousers.data)
for height,normal in [(.947,(0,0,1)),(.087,(0,0,-1))]:
    bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,plane_co=(0,0,height),plane_no=normal,clear_outer=True)
bm.to_mesh(trousers.data);bm.free()
# Release temporary sampling arrays before voxel welding the hands/arms to this body.
del X,Y,Z,field,values,low,high,cell,cv,cp,tris,triangles,verts,indices,faces
