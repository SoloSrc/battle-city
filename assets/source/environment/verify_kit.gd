extends SceneTree
var failures: Array[String]=[]
func check(ok:bool, message:String):
 if not ok:failures.append(message);push_error(message)
func _initialize():call_deferred("run")
func run():
 var manifest=JSON.parse_string(FileAccess.get_file_as_string("res://assets/source/environment/manifest.json"))
 var passed=0
 for entry in manifest:
  var packed=load("res://"+entry.path) as PackedScene
  check(packed!=null,entry.name+": loads")
  if packed==null:continue
  var instance=packed.instantiate();root.add_child(instance)
  var meshes=instance.find_children("*","MeshInstance3D",true,false)
  check(meshes.size()==1,entry.name+": one visible mesh")
  var collisions=instance.find_children("*","CollisionShape3D",true,false)
  check((collisions.size()>0)==entry.collision,entry.name+": collision contract")
  if meshes.size()>0:
   var m=meshes[0] as MeshInstance3D
   var bounds=m.transform*m.get_aabb()
   var expected=Vector3(entry.size_xyz_m[0],entry.size_xyz_m[1],entry.size_xyz_m[2])
   check((bounds.size-expected).length()<.002,entry.name+": dimensions in metres")
   check(abs(bounds.position.y)<.002,entry.name+": feet/bounds bottom at y=0")
   if entry.name.begins_with("kit_"):check(bounds.position.length()<.002,entry.name+": kit bounds-min origin")
   for i in range(m.mesh.get_surface_count()):
    var material=m.mesh.surface_get_material(i)
    check(material!=null and material.resource_name.begins_with("toon_"),entry.name+": toon material name")
  instance.queue_free();await process_frame;passed+=1
 # Check the actual generated doorway collision, not only bounding boxes.
 var world=Node3D.new();root.add_child(world)
 var door=load("res://assets/kit/kit_wall_door.glb").instantiate();world.add_child(door)
 await physics_frame;await physics_frame
 var space=world.get_world_3d().direct_space_state
 for x in [1.05,2.0,2.95]:
  var q=PhysicsRayQueryParameters3D.create(Vector3(x,1,-1),Vector3(x,1,1))
  check(space.intersect_ray(q).is_empty(),"2 m doorway has no hidden collider at x="+str(x))
 var jamb=PhysicsRayQueryParameters3D.create(Vector3(.5,1,-1),Vector3(.5,1,1))
 check(not space.intersect_ray(jamb).is_empty(),"door jamb collides")
 # Two exported 2 m slabs on the grid must meet exactly.
 var tile=load("res://assets/kit/kit_ground_street.glb").instantiate();world.add_child(tile)
 var mesh=tile.find_children("*","MeshInstance3D",true,false)[0]
 var bb=mesh.transform*mesh.get_aabb()
 check(abs(bb.end.x-(bb.position.x+2))<.0001,"ground tiles meet on 2 m spacing")
 print("KIT32: ",passed," assets checked; ",failures.size()," failures. ",failures)
 world.queue_free();await process_frame
 quit(0 if failures.is_empty() else 1)
