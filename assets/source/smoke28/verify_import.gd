extends SceneTree
var failures: Array[String]=[]
func check(ok:bool, label:String):
 print("PASS " if ok else "FAIL ",label)
 if not ok: failures.append(label)
func _initialize():call_deferred("run")
func run():
 var paths={"cube":"res://assets/kit/kit_test_cube_1m.glb","body":"res://assets/characters/body/char_a_body.glb","anims":"res://assets/characters/anims/character_anims.glb","disk":"res://assets/props/prop_duel_disk.glb"}
 var instances={}
 for key in paths:
  var scene=load(paths[key]) as PackedScene
  check(scene!=null,key+" resource loads")
  if scene==null:continue
  var n=scene.instantiate();root.add_child(n);instances[key]=n
  var meshes=n.find_children("*","MeshInstance3D",true,false)
  var tris=0
  for mi in meshes:
   for surf in mi.mesh.get_surface_count():
    var arr=mi.mesh.surface_get_arrays(surf)
    tris+=arr[Mesh.ARRAY_INDEX].size()/3 if arr[Mesh.ARRAY_INDEX]!=null else arr[Mesh.ARRAY_VERTEX].size()/3
  print(key," triangles=",tris)
  if key=="cube":
   check(meshes.size()==1,"cube has one render mesh")
   check(n.find_children("*","StaticBody3D",true,false).size()==1,"cube collision body")
   var bounds: AABB=meshes[0].global_transform*meshes[0].get_aabb()
   check(bounds.size.is_equal_approx(Vector3.ONE) and bounds.position.is_equal_approx(Vector3.ZERO),"cube metre size and minimum-corner origin")
  if key in ["body","anims"]:
   check(tris<=12000,"character under triangle budget")
   check(abs(meshes[0].get_aabb().size.y-1.7)<.01,"character height 1.7m")
   var sk=n.find_children("*","Skeleton3D",true,false)[0]
   check(sk.find_bone("LeftLowerArm")>=0,"LeftLowerArm bone")
  if key=="disk":
   check(tris<=1500,"disk under triangle budget")
   check(meshes[0].mesh.get_surface_count()==1,"disk single material")
   for marker in ["deck","graveyard","banished","bay_1","bay_2","bay_3","bay_4","bay_5"]:check(n.find_child(marker,true,false)!=null,"disk marker "+marker)
 var body=instances["body"]
 var carrier=instances["anims"]
 var skeleton:Skeleton3D=body.find_children("*","Skeleton3D",true,false)[0]
 var src:Skeleton3D=carrier.find_children("*","Skeleton3D",true,false)[0]
 check(skeleton.get_bone_count()==src.get_bone_count(),"shared skeleton bone count")
 for i in skeleton.get_bone_count():check(skeleton.get_bone_name(i)==src.get_bone_name(i) and skeleton.get_bone_rest(i).is_equal_approx(src.get_bone_rest(i)),"shared bone/rest "+skeleton.get_bone_name(i))
 var ap:AnimationPlayer=carrier.find_children("*","AnimationPlayer",true,false)[0]
 for pair in [["idle",4.0],["walk",1.0]]:
  var a=ap.get_animation(pair[0]);check(is_equal_approx(a.length,pair[1]),pair[0]+" duration");check(a.loop_mode==Animation.LOOP_LINEAR,pair[0]+" loops")
 # Use AnimationTree to evaluate the exported walk without controller code.
 var tree=AnimationTree.new();carrier.add_child(tree);tree.anim_player=tree.get_path_to(ap)
 var animnode=AnimationNodeAnimation.new();animnode.animation="walk";tree.tree_root=animnode;tree.active=true
 var leg=src.find_bone("LeftUpperLeg")
 tree.advance(.10);var before=src.get_bone_pose_rotation(leg);tree.advance(.30);var after=src.get_bone_pose_rotation(leg)
 check(not before.is_equal_approx(after),"walk evaluates through AnimationTree")
 var root_idx=src.find_bone("Root");check(src.get_bone_pose_position(root_idx).is_zero_approx(),"no Root translation")
 # Candidate body-A mount in rest pose. X blade points outward; Y dorsal normal faces -Z.
 var rest=src.get_bone_global_rest(src.find_bone("LeftLowerArm"))
 var desired=Transform3D(Basis(Vector3(.8384436,.5449884,0),Vector3(0,0,-1),Vector3(-.5449884,.8384436,0)),Vector3(.405,1.03,-.06))
 var offset=rest.affine_inverse()*desired
 print("BODY_A_MOUNT_OFFSET ",offset)
 var attachment=BoneAttachment3D.new();src.add_child(attachment);attachment.bone_name="LeftLowerArm"
 var mount=Node3D.new();attachment.add_child(mount);mount.transform=offset
 var disk=instances["disk"];disk.reparent(mount,false)
 var dp:AnimationPlayer=disk.find_children("*","AnimationPlayer",true,false)[0]
 var bay=disk.find_child("bay_5",true,false) as Node3D
 dp.play("disk_deploy");dp.pause();dp.seek(0,true);await process_frame;var folded=bay.global_position
 dp.seek(.6,true);await process_frame;var deployed=bay.global_position
 print("BAY_POSES ",folded," ",deployed)
 check(folded.distance_to(deployed)>.2,"disk hinge moves bay anchors between poses")
 await process_frame
 var first=disk.global_position;tree.advance(.2);await process_frame
 check(first.distance_to(disk.global_position)>.001,"disk follows LeftLowerArm through animation")
 print("FAILURES ",failures)
 for n in instances.values():if is_instance_valid(n) and n.get_parent()==root:n.queue_free()
 await process_frame
 quit(0 if failures.is_empty() else 1)
