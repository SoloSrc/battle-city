extends SceneTree
func _initialize():
 var count=0
 for folder in ["frames","icons"]:
  for file in DirAccess.get_files_at("res://assets/cards/"+folder):
   if not file.ends_with(".png"):continue
   var t=load("res://assets/cards/"+folder+"/"+file) as Texture2D
   if t==null:push_error("Missing texture "+file);quit(1);return
   var expected=Vector2i(590,860) if folder=="frames" else Vector2i(64,64) if file=="star.png" else Vector2i(128,128)
   if Vector2i(t.get_size())!=expected:push_error("Size mismatch "+file);quit(1);return
   var material=StandardMaterial3D.new()
   material.albedo_texture=t
   material.transparency=BaseMaterial3D.TRANSPARENCY_ALPHA
   if material.albedo_texture!=t:quit(1);return
   count+=1
 print("CARD_TEXTURE_CHECK ",count," imported textures, expected sizes and StandardMaterial3D bindings passed. Shared hologram shader pending #27.")
 quit(0 if count==22 else 1)
