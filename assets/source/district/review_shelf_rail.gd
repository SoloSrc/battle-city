extends SceneTree
func _initialize():call_deferred('run')
func run():
 root.size=Vector2i(1200,800)
 var game=root.get_node('Game')
 var suffix='after' if '--after' in OS.get_cmdline_user_args() else 'before'
 var folder='res://docs/art/previews/shelf-rail-fix'
 DirAccess.make_dir_recursive_absolute(folder)
 for sample in [['shop','res://levels/district/interiors/ShopInterior.tscn',Vector3(5,0,2)],['rail-north','res://levels/district/District.tscn',Vector3(110,0,41)],['rail-south','res://levels/district/District.tscn',Vector3(110,0,103)]]:
  game.call('Transition',sample[1],'arrival')
  for i in 100:await process_frame
  while game.get('IsTransitioning'):await process_frame
  game.call('LockInput','geometry_review')
  game.get('Encounters').process_mode=Node.PROCESS_MODE_DISABLED
  game.get('Player').position=sample[2]
  for node in root.find_children('*','Node3D',true,false):
   if node.has_method('Snap') and node.has_node('Pivot/Camera3D'):node.call('Snap')
  for i in 15:await process_frame
  RenderingServer.force_draw(false)
  print('CAPTURE ',sample[0],' ',root.get_texture().get_image().save_png(folder+'/'+sample[0]+'-'+suffix+'.png'))
 quit()
