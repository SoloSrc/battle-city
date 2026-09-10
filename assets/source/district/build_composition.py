"""Rebuild artist-owned, script-free district scenes (Python 3, no dependencies).

Run from the repository root. Bake navigation with bake_navigation.gd afterwards.
World coordinates follow docs/design/district-layout.md; kit instances retain scale 1.
"""
from pathlib import Path
import math

ROOT = Path.cwd()

def vec(v):
    return 'Vector3(%s)' % ', '.join(str(round(x, 6)) for x in v)

class Scene:
    def __init__(self, name, kind='Node3D'):
        self.name, self.exts, self.subs, self.nodes, self.ids = name, {}, [], [], 0
        self.node(name, kind, None)
    def ext(self, path, kind='PackedScene'):
        if path not in self.exts: self.exts[path] = (str(len(self.exts)+1), kind)
        return 'ExtResource("%s")' % self.exts[path][0]
    def sub(self, kind, props):
        self.ids += 1
        key = 'r%d' % self.ids
        self.subs.append('[sub_resource type="%s" id="%s"]\n%s' % (kind,key,props))
        return 'SubResource("%s")' % key
    def node(self, name, kind=None, parent='.', props='', instance=None, extra=''):
        attrs = 'name="%s"' % name
        if kind: attrs += ' type="%s"' % kind
        if parent is not None: attrs += ' parent="%s"' % parent
        if instance: attrs += ' instance=%s' % instance
        self.nodes.append('[node %s%s]\n%s' % (attrs, extra, props))
    def instance(self,name,path,pos=(0,0,0),yaw=0,props='',parent='.'):
        self.node(name,parent=parent,instance=self.ext(path),props='position = %s\nrotation = %s\n%s' % (vec(pos),vec((0,math.radians(yaw),0)),props))
    def kit(self, asset,x,z,y=0,yaw=0):
        folder = 'props' if asset.startswith('prop_') else 'kit'
        self.instance(asset+'_%03d'%len(self.nodes),'res://assets/%s/%s.glb'%(folder,asset),(x,y,z),yaw)
    def box(self,name,pos,size,color, surface=None,collide=True):
        mat=self.sub('StandardMaterial3D','albedo_color = Color(%s, 1)\nroughness = 0.95' % ', '.join(map(str,color)))
        mesh=self.sub('BoxMesh','size = %s\nmaterial = %s'%(vec(size),mat))
        props='position = %s'%vec(pos)
        if surface is not None: props+='\nscript = %s\nKind = %d'%(self.ext('res://src/World/SurfaceTag.cs','Script'),surface)
        self.node(name,'StaticBody3D' if collide else 'Node3D',props=props)
        self.node('Mesh','MeshInstance3D',name,'mesh = '+mesh)
        if collide:
            shape=self.sub('BoxShape3D','size = '+vec(size))
            self.node('Collision','CollisionShape3D',name,'shape = '+shape)
    def floor(self,name,bounds,color,surface=0):
        x0,x1,z0,z1=bounds
        self.box(name,((x0+x1)/2,-.125,(z0+z1)/2),(x1-x0,.25,z1-z0),color,surface)
    def zone(self,area,bounds,interior=False):
        x0,x1,z0,z1=bounds; pos=((x0+x1)/2,2,(z0+z1)/2)
        self.instance('CameraBounds','res://scenes/world/CameraBounds.tscn',pos,props='Size = %s\nInterior = %s\nPitch = 57.0\nDistance = %s' % (vec((x1-x0+4,12,z1-z0+4)),str(interior).lower(),'7.0' if interior else '12.0'))
        self.instance('AmbientZone','res://scenes/world/AmbientZone.tscn',pos,props='Size = %s\nmetadata/area_id = "%s"'%(vec((x1-x0,10,z1-z0)),area))
    def spawn(self,name,identifier,pos,yaw=0):
        self.instance(name,'res://scenes/world/PlayerSpawn.tscn',pos,yaw,'Id = "%s"'%identifier)
    def door(self,name,pos,target,spawn,ret,yaw=0):
        self.instance(name,'res://scenes/world/Door.tscn',pos,yaw,'TargetScene = "%s"\nTargetSpawn = "%s"\nReturnSpawn = "%s"'%(target,spawn,ret))
    def npc(self,name,pos,ident,duelist=False,yaw=180,locked=False):
        self.instance(name,'res://scenes/world/%s.tscn'%('Duelist' if duelist else 'TalkNpc'),pos,yaw)
        self.node('Marker' if duelist else 'Talk',parent=name,extra=' index="5"',props=('%s = "%s"'%('DuelistId' if duelist else 'DialogueId',ident))+ ('\nArmed = false\nChallengeable = false' if locked else ''))
    def site(self,name,pos,ident):
        self.instance(name,'res://scenes/world/EncounterSite.tscn',pos,props='Id = "%s"\nDuelistId = "%s"'%(name.lower(),ident))
    def boundary(self,name,x0,z0,x1,z1,asset='kit_bound_hedge'):
        # Continuous blocking backing closes fractional seams; kit keeps its authored dimensions.
        dx,dz=x1-x0,z1-z0; length=abs(dx)+abs(dz)
        self.box(name,((x0+x1)/2,.75,(z0+z1)/2),(max(abs(dx),.75),1.5,max(abs(dz),.75)),(.30,.43,.27))
        for i in range(int(length//2)):
            if dx: self.kit(asset,x0+2*i,z0-.375)
            else: self.kit(asset,x0-.375,z0+2*i+2,yaw=90)
    def building(self,name,x,z,w,d,door=False,arcade=False):
        # Solid exterior mass: transition doors are portals, not traversable exterior interiors.
        self.box(name,(x+w/2,1.75,z+d/2),(w,3.5,d),(.69,.62,.49))
        for i in range(0,w,2): self.kit('kit_wall_window',x+i,z+d,y=0)
        for i in range(0,w,2):
            for j in range(0,d,2): self.kit('kit_roof_flat_warm',x+i,z+j,y=3.5)
        if door:
            self.kit('kit_shop_awning',x+w/2-2,z+d,y=2.5)
            self.kit('kit_shop_sign',x+w/2-1,z+d+.05,y=3.05)
        if arcade:
            self.kit('kit_arcade_closed_doors',x+w/2-2,z+d)
            self.kit('kit_arcade_marquee',x+w/2-2,z+d,y=2.5)
            self.kit('kit_arcade_sign',x+w/2-2,z+d,y=3.2)
    def save(self,path):
        p=ROOT/path; p.parent.mkdir(parents=True,exist_ok=True)
        ext=['[ext_resource type="%s" path="%s" id="%s"]'%(kind,path,key) for path,(key,kind) in self.exts.items()]
        p.write_text('\n\n'.join(['[gd_scene load_steps=%d format=3]'%(1+len(ext)+len(self.subs))]+ext+self.subs+self.nodes).rstrip()+'\n')

areas={}
s=Scene('Plaza'); areas['Plaza']=s
s.floor('PlazaPaving',(40,76,66,104),(.60,.57,.49)); s.zone('plaza',(40,76,66,104))
s.spawn('Arrival','arrival',(58,0,98)); s.spawn('RoomReturn','room_return',(58,0,101),180)
s.door('RoomDoor',(58,0,103.7),'res://levels/district/interiors/StartingRoom.tscn','arrival','room_return',180)
s.site('NicoSite',(58,0,88),'d1'); s.npc('Nico',(58,0,91),'d1',True)
s.kit('prop_fountain',66,70)
for x,z in [(44,72),(70,96)]: s.kit('prop_bench',x,z)
for x,z in [(42,98),(72,78)]: s.kit('prop_lamp_post',x,z)
s.building('StartingRoomExterior',52,104,12,6)
s.kit('kit_wall_door',56,103.75)

s=Scene('Market'); areas['Market']=s
s.floor('MarketPaving',(8,40,48,104),(.50,.51,.49)); s.zone('market',(8,40,48,104))
s.building('CardShop',16,63,12,10,door=True)
s.spawn('ShopReturn','shop_return',(22,0,76),180)
s.door('CardShopDoor',(22,0,73.5),'res://levels/district/interiors/CardShop.tscn','arrival','shop_return')
s.npc('MarketResident',(30,0,78),'market_resident'); s.npc('CardCollector',(14,0,82),'card_collector',yaw=90)
s.building('NorthMarketFacade',10,48,18,6)
s.building('SouthMarketFacade',10,94,18,10)
for x,z in [(12,76),(34,90),(34,54)]: s.kit('prop_planter_large',x,z)

s=Scene('Park'); areas['Park']=s
s.floor('ParkGrass',(76,112,40,104),(.39,.49,.31),1); s.zone('park',(76,112,40,104))
# Path replaces grass at the same height, with a thinner top layer and tagged stone collision.
s.box('RiversidePath',(94,.005,70),(12,.01,60),(.63,.57,.44),0)
s.box('EntryPath',(82,.005,88),(12,.01,6),(.63,.57,.44),0)
s.site('MaraSite',(94,0,68),'d2'); s.npc('Mara',(94,0,71),'d2',True,locked=True)
for x,z in [(80,48),(80,62),(80,96),(106,48),(106,78),(104,96)]: s.kit('prop_tree_round',x,z)
for x,z in [(103,60),(103,88)]: s.kit('prop_bench',x,z,yaw=90)
s.box('River',(116,-.18,72),(8,.08,64),(.29,.47,.52),collide=False)

s=Scene('Arcade'); areas['Arcade']=s
s.floor('ArcadePaving',(40,100,8,40),(.49,.48,.47)); s.zone('arcade',(40,100,8,40))
s.building('OldArcade',62,8,16,10,arcade=True)
s.site('ArcadeSite',(70,0,26),'d3'); s.npc('ArcadeOwner',(74,0,27),'d3',True,yaw=135,locked=True)
for x,z in [(46,14),(84,14)]: s.kit('prop_planter_large',x,z)

s=Scene('Edge'); areas['Edge']=s
s.zone('edge',(30,40,48,64))
s.instance('LevelBounds','res://scenes/world/LevelBounds.tscn',(60,0,60))
for name,coords in {
 'SouthWest':(8,104,57,104),'SouthEast':(59,104,112,104),'MarketWest':(8,48,8,104),'MarketNorth':(8,48,40,48),
 'PlazaNorth':(40,66,76,66),'MarketUpperEast':(40,48,40,66),
 'ArcadeWest':(40,8,40,40),'ArcadeNorth':(40,8,100,8),'ArcadeEast':(100,8,100,40),
 'ArcadeSouthWest':(40,40,92,40),'ArcadeSouthEast':(96,40,112,40),
 'ParkDivideNorth':(76,40,76,86),'ParkDivideSouth':(76,90,76,104),
 'RiverRail':(112,40,112,104)}.items():
    s.boundary(name,*coords,asset='kit_bound_river_edge' if name=='RiverRail' else 'kit_bound_hedge')
s.instance('ParkGate','res://scenes/world/ProgressionGate.tscn',(76,0,88),90,'RequiredFlag = "defeated:d1"')
s.instance('ArcadeGate','res://scenes/world/ProgressionGate.tscn',(94,0,40),props='RequiredFlag = "defeated:d2"')
s.npc('ConstructionWorker',(36,0,58),'district_edge',yaw=180)
for x in (30,32,34,36,38): s.kit('kit_bound_construction',x,48)

for name,s in areas.items(): s.save('levels/district/areas/%s.tscn'%name)

def light_and_nav(s,navpath):
    nav=s.ext(navpath,'NavigationMesh')
    s.node('Navigation','NavigationRegion3D',props='navigation_mesh = '+nav)
    env=s.sub('Environment','background_mode = 1\nbackground_color = Color(0.61, 0.70, 0.76, 1)\nambient_light_source = 2\nambient_light_color = Color(0.83, 0.88, 1, 1)\nambient_light_energy = 0.2\ntonemap_mode = 0')
    s.node('WorldEnvironment','WorldEnvironment',props='environment = '+env)
    s.node('Sun','DirectionalLight3D',props='rotation_degrees = Vector3(-65, -25, 0)\nlight_energy = 0.3\nshadow_enabled = true\ndirectional_shadow_max_distance = 25.0')

# Independent assembly while Fable owns the #23 runtime District root.
s=Scene('DistrictComposition')
light_and_nav(s,'res://levels/district/navigation/DistrictReview.tres')
for name in areas: s.instance(name,'res://levels/district/areas/%s.tscn'%name,parent='Navigation')
s.instance('Player','res://scenes/characters/Player.tscn',(58,0,98))
s.instance('CameraRig','res://scenes/world/CameraRig.tscn')
s.save('levels/review/DistrictComposition.tscn')

for name,w,d in [('StartingRoom',8,6),('CardShop',10,8)]:
    s=Scene(name); light_and_nav(s,'res://levels/district/navigation/%s.tres'%name)
    # Geometry belongs under Navigation; zone/door/spawn can remain root children.
    start=len(s.nodes)
    s.floor('WoodFloor',(0,w,0,d),(.55,.42,.29),2)
    s.box('NorthWall',(w/2,1.75,-.125),(w,3.5,.25),(.69,.65,.54))
    s.box('WestWall',(-.125,1.75,d/2),(.25,3.5,d),(.69,.65,.54))
    s.box('EastWall',(w+.125,1.75,d/2),(.25,3.5,d),(.69,.65,.54))
    # South wall low enough for the fixed-yaw camera; physical doorway is 2 m wide.
    for label,x in [('Left',(w/2-1)/2),('Right',(w/2+1+w)/2)]:
        s.box('South'+label,(x,.4,d+.125),(w/2-1,.8,.25),(.69,.65,.54))
    if name=='StartingRoom':
        s.kit('kit_int_bed',.75,.5); s.kit('kit_int_desk',w-2,.5)
    else:
        for x in (1,3,5,7): s.kit('kit_int_counter',x,1.5)
        for x in (1,5,7): s.kit('kit_int_shelves',x,0)
        s.kit('kit_int_card_display',1,4)
    for i in range(start,len(s.nodes)):
        s.nodes[i]=s.nodes[i].replace('parent="."','parent="Navigation"')
        if 'parent="Navigation"' not in s.nodes[i]:
            s.nodes[i]=s.nodes[i].replace('parent="','parent="Navigation/')
    s.zone('room' if name=='StartingRoom' else 'shop',(0,w,0,d),True)
    s.instance('LevelBounds','res://scenes/world/LevelBounds.tscn',(w/2,0,d/2),props='Size = %s'%vec((w+2,12,d+2)))
    s.spawn('Arrival','arrival',(w/2,0,d-1.5)); s.spawn('DoorReturn','exit_return',(w/2,0,d-1.5))
    s.door('Exit',(w/2,0,d),'res://levels/review/DistrictComposition.tscn','room_return' if name=='StartingRoom' else 'shop_return','exit_return',180)
    if name=='CardShop':
        s.instance('ShopCounter','res://scenes/world/ShopCounter.tscn',(5,0,2.5))
        s.node('Placeholder',parent='ShopCounter',extra=' index="1"',props='visible = false')
    s.save('levels/district/interiors/%s.tscn'%name)

# Empty resources bootstrap scene import; bake_navigation.gd replaces them.
for name in ('DistrictReview','StartingRoom','CardShop'):
    p=ROOT/'levels/district/navigation'/('%s.tres'%name); p.parent.mkdir(parents=True,exist_ok=True)
    p.write_text('[gd_resource type="NavigationMesh" format=3]\n\n[resource]\nagent_radius = 0.4\nagent_height = 1.7\nagent_max_climb = 0.2\ngeometry_parsed_geometry_type = 1\ngeometry_collision_mask = 1\n')
