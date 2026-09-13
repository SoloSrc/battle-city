extends SceneTree
var out := "res://docs/art/previews/shader-review"
func _initialize() -> void: call_deferred("run")
func shot(name: String) -> void:
    for i in 10: await process_frame
    RenderingServer.force_draw(false)
    var result := root.get_texture().get_image().save_png(out+"/"+name+".png")
    print("REVIEW ",name," save=",result)
func run() -> void:
    root.size=Vector2i(1200,800)
    DirAccess.make_dir_recursive_absolute(out)
    if "--framing" in OS.get_cmdline_user_args() or "--smoke" in OS.get_cmdline_user_args():
        var framing="--framing" in OS.get_cmdline_user_args()
        root.add_child(load("res://tests/scenes/"+("CameraFraming" if framing else "SmokeTest")+".tscn").instantiate())
        for i in 60:await process_frame
        await shot("camera-framing" if framing else "smoke")
        quit();return
    var game=root.get_node("Game")
    game.call("Transition","res://levels/district/District.tscn","arrival")
    for i in 100: await process_frame
    while game.get("IsTransitioning"): await process_frame
    var player:Node3D=game.get("Player")
    player.position=Vector3(22,0,75)
    # CameraRig is owned by World; locate the actual runtime rig.
    var rigs=root.find_children("*","Node3D",true,false)
    for node in rigs:
        if node.has_method("Snap") and node.has_node("Pivot/Camera3D"):node.call("Snap")
    game.get("Level").get_node("Sun").light_energy=.3
    game.get("Level").get_node("WorldEnvironment").environment.ambient_light_energy=.2
    await shot("district-before")
    var level:Node3D=game.get("Level")
    level.get_node("Sun").light_energy=1.0
    level.get_node("WorldEnvironment").environment.ambient_light_energy=.35
    await shot("district-daylight")
    for canvas in game.find_children("*","CanvasLayer",true,false):canvas.visible=false
    game.process_mode=Node.PROCESS_MODE_DISABLED
    root.get_node("World").free()
    var world=Node3D.new();root.add_child(world)
    var env=Environment.new();env.background_mode=Environment.BG_COLOR;env.background_color=Color(.45,.53,.61)
    env.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR;env.ambient_light_color=Color(.83,.88,1);env.ambient_light_energy=.35
    var we=WorldEnvironment.new();we.environment=env;world.add_child(we)
    var sun=DirectionalLight3D.new();sun.rotation_degrees=Vector3(-45,-30,0);sun.light_energy=1.0;world.add_child(sun)
    var actor:Node3D=load("res://scenes/characters/Character.tscn").instantiate();world.add_child(actor)
    actor.get_node("AnimationTree").active=false
    var camera=Camera3D.new();world.add_child(camera);camera.position=Vector3(1.4,1.2,4);camera.look_at(Vector3(.4,.85,0));camera.projection=Camera3D.PROJECTION_ORTHOGONAL;camera.size=2.6;camera.look_at(Vector3(.8,.85,0));camera.make_current()
    var face=Image.load_from_file(ProjectSettings.globalize_path("res://assets/cards/art/blade_knight.png"))
    face.resize(560,560)
    var composed=Image.create(590,860,false,Image.FORMAT_RGBA8);composed.fill(Color.TRANSPARENT)
    composed.blit_rect(face,Rect2i(20,0,520,560),Vector2i(35,40))
    composed.blend_rect(Image.load_from_file(ProjectSettings.globalize_path("res://assets/cards/frames/frame_effect.png")),Rect2i(0,0,590,860),Vector2i.ZERO)
    var star=Image.load_from_file(ProjectSettings.globalize_path("res://assets/cards/icons/star.png"));star.resize(32,32)
    for i in 4:composed.blend_rect(star,Rect2i(0,0,32,32),Vector2i(52+i*34,640))
    var attr=Image.load_from_file(ProjectSettings.globalize_path("res://assets/cards/icons/attr_light.png"));attr.resize(48,48)
    composed.blend_rect(attr,Rect2i(0,0,48,48),Vector2i(490,632))
    # Draw stat numbers with Godot font rendering in an offscreen viewport.
    var numbers=SubViewport.new();numbers.size=Vector2i(590,860);numbers.transparent_bg=true;numbers.render_target_update_mode=SubViewport.UPDATE_ALWAYS;root.add_child(numbers)
    for item in [[68,"1600"],[338,"1000"]]:
        var label=Label.new();label.text=item[1];label.position=Vector2(item[0],726)
        label.add_theme_font_size_override("font_size",56);label.add_theme_color_override("font_color",Color(.15,.12,.10));numbers.add_child(label)
    for i in 3:await process_frame
    RenderingServer.force_draw(false)
    composed.blend_rect(numbers.get_texture().get_image(),Rect2i(0,0,590,860),Vector2i.ZERO)
    numbers.free()
    for mesh in actor.find_children("*","MeshInstance3D",true,false):
        for i in mesh.mesh.get_surface_count():
            var mat=mesh.get_active_material(i)
            if mat is ShaderMaterial and mat.shader.resource_path.ends_with("toon.gdshader"):
                mat.set_shader_parameter("rim_strength",.35);mat.set_shader_parameter("specular_strength",.05)
    var cards=[]
    for i in 3:
        var card=MeshInstance3D.new();var quad=QuadMesh.new();quad.size=Vector2(.5,.5*860/590);card.mesh=quad
        var mat=load("res://shaders/materials/hologram_"+("opponent" if i==1 else "player")+".tres").duplicate()
        mat.set_shader_parameter("face_texture",ImageTexture.create_from_image(composed));mat.set_shader_parameter("hover_amplitude",0.0)
        mat.set_shader_parameter("face_opacity",.88);mat.set_shader_parameter("tint_strength",.12);mat.set_shader_parameter("scanline_strength",.16)
        card.material_override=mat;card.position=Vector3(.6+i*.62,1.05,0);world.add_child(card)
        if i==2:card.rotation.y=PI
        cards.append(card)
    await shot("character-cards-before")
    for mesh in actor.find_children("*","MeshInstance3D",true,false):
        for i in mesh.mesh.get_surface_count():
            var mat=mesh.get_active_material(i)
            if mat is ShaderMaterial and mat.shader.resource_path.ends_with("toon.gdshader"):
                mat.set_shader_parameter("rim_strength",.15);mat.set_shader_parameter("specular_strength",0.0)
    for card in cards:
        card.material_override.set_shader_parameter("face_opacity",.97)
        card.material_override.set_shader_parameter("tint_strength",.06)
        card.material_override.set_shader_parameter("scanline_strength",.08)
    await shot("character-cards-tuned")
    cards[0].set_instance_shader_parameter("selected",1.0)
    cards[1].set_instance_shader_parameter("reveal",.5)
    cards[2].set_instance_shader_parameter("dissolve",1.0)
    await shot("card-states")
    print("REVIEW renderer=",RenderingServer.get_current_rendering_method())
    quit()
