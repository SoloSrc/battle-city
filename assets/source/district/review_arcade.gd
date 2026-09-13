extends SceneTree
func _initialize():call_deferred('run')
func run():
 root.size=Vector2i(1200,800)
 var game=root.get_node('Game')
 game.call('Transition','res://levels/district/District.tscn','arrival')
 for i in 100:await process_frame
 while game.get('IsTransitioning'):await process_frame
 game.call('LockInput','review69')
 game.get('Encounters').process_mode=Node.PROCESS_MODE_DISABLED
 var folder='res://docs/art/previews/arcade-geometry'
 DirAccess.make_dir_recursive_absolute(folder)
 for sample in [['after',Vector3(70,0,22)],['overview',Vector3(70,0,19)],['left',Vector3(67,0,20)],['right',Vector3(73,0,20)],['no-shadows',Vector3(70,0,22)]]:
  game.get('Player').position=sample[1]
  for node in root.find_children('*','Node3D',true,false):
   if node.has_method('Snap') and node.has_node('Pivot/Camera3D'):node.call('Snap')
  if sample[0]=='no-shadows':
   for sun in root.find_children('*','DirectionalLight3D',true,false):sun.shadow_enabled=false
  for i in 15:await process_frame
  RenderingServer.force_draw(false)
  var result=root.get_texture().get_image().save_png(folder+'/'+sample[0]+'.png')
  print('ARCADE capture ',sample[0],' save=',result)
 quit()
