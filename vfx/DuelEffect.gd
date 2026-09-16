class_name DuelArtEffect
extends Node3D
## Original SOLOSRC MIT effect asset. Runtime hook adapter belongs to Fable.
signal finished
@export_enum("CardMaterialise", "CardSelected", "SummonFlash", "AttackTrail", "HitPulse", "Dissolve") var effect := "SummonFlash"
@export var duration := 0.5
@export var color := Color("79d8ff")
@export var autoplay := true
var card: GeometryInstance3D
var elapsed := 0.0
var active := false
var destination := Vector3.ZERO
var has_destination := false
var ink: StandardMaterial3D
var pieces: Array[MeshInstance3D] = []
# Summon geometry fits a 0.22 m card zone; damage geometry is character-sized.
const SUMMON_RADIUS := 0.045
const HIT_RADIUS := 0.22

## Call after adding the scene to the tree and before the deferred autoplay.
func configure(from_anchor: Transform3D, to_anchor: Transform3D) -> void:
    global_transform = from_anchor
    destination = to_anchor.origin
    has_destination = true

## Bind an actual HologramCards/CardView quad for the three card-state effects.
func bind_card(target: GeometryInstance3D) -> void:
    card = target

func _ready() -> void:
    ink = StandardMaterial3D.new()
    ink.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
    ink.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
    ink.albedo_color = color
    ink.emission_enabled = true
    ink.emission = color
    ink.emission_energy_multiplier = 0.6
    ink.cull_mode = BaseMaterial3D.CULL_DISABLED
    if effect in ["SummonFlash", "HitPulse"]:
        var ring := TorusMesh.new()
        ring.outer_radius = SUMMON_RADIUS if effect == "SummonFlash" else HIT_RADIUS
        ring.inner_radius = 0.035 if effect == "SummonFlash" else 0.18
        ring.rings = 32; ring.ring_segments = 8
        add_piece(ring)
        if effect == "HitPulse": pieces[0].rotation.x = PI/2
        for i in 8:
            var ray := BoxMesh.new()
            ray.size = Vector3(.008,.008,.025) if effect == "SummonFlash" else Vector3(.025,.16,.025)
            add_piece(ray)
    elif effect == "AttackTrail":
        var ribbon := CylinderMesh.new()
        ribbon.top_radius=.015; ribbon.bottom_radius=.045; ribbon.height=1.0
        ribbon.radial_segments=8
        add_piece(ribbon)
        var head := SphereMesh.new();head.radius=.055;head.height=.11;head.radial_segments=12;head.rings=6
        add_piece(head)
    if autoplay: call_deferred("play")

func add_piece(mesh: Mesh) -> void:
    var piece := MeshInstance3D.new();piece.mesh=mesh;piece.material_override=ink
    piece.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
    add_child(piece);pieces.append(piece)

func play() -> void:
    elapsed=0.0;active=true
    if not has_destination: destination=global_position+global_basis.x*1.5
    if effect in ["CardMaterialise","CardSelected","Dissolve"] and not is_instance_valid(card):
        push_warning(effect+" requires bind_card(GeometryInstance3D); finishing without a target")
        complete();return
    if effect=="CardMaterialise":card.set_instance_shader_parameter("reveal",0.0)
    if effect=="CardSelected":card.set_instance_shader_parameter("selected",1.0)
    if effect=="Dissolve":card.set_instance_shader_parameter("dissolve",0.0)

func _process(delta: float) -> void:
    if not active:return
    if effect=="CardSelected":
        if not is_instance_valid(card):complete()
        return
    elapsed+=delta
    var t := clampf(elapsed/maxf(duration,.001),0.0,1.0)
    if effect in ["CardMaterialise","Dissolve"]:
        if not is_instance_valid(card):complete();return
        card.set_instance_shader_parameter("reveal" if effect=="CardMaterialise" else "dissolve",t)
    elif effect in ["SummonFlash","HitPulse"]:
        var radius := lerpf(.35,2.1,t)
        pieces[0].scale=Vector3.ONE*radius
        for i in 8:
            var a := TAU*i/8.0
            var base_radius := SUMMON_RADIUS if effect == "SummonFlash" else HIT_RADIUS
            var offset := Vector3(cos(a),0,sin(a))*base_radius*radius
            if effect=="HitPulse":offset=Vector3(offset.x,offset.z,0)
            pieces[i+1].position=offset
            if effect == "HitPulse":
                pieces[i+1].rotation.z=a-PI/2
            else:
                pieces[i+1].rotation.y=PI/2-a
            pieces[i+1].scale=Vector3.ONE*(1.0-t*.6)
        ink.albedo_color=Color(color,1.0-t)
        ink.emission_energy_multiplier=.6*(1.0-t)
    elif effect=="AttackTrail":
        var end := to_local(destination)
        var head := end*t
        var tail := end*maxf(0.0,t-.32)
        var direction := head-tail
        pieces[0].position=(head+tail)*.5
        pieces[0].visible=direction.length()>.0001
        if pieces[0].visible:
            pieces[0].quaternion=Quaternion(Vector3.UP,direction.normalized())
            pieces[0].scale=Vector3(1.0-t*.5,direction.length(),1.0-t*.5)
        pieces[1].position=head
        ink.albedo_color=Color(color,1.0-t*.5)
    if t>=1.0:complete()

## Stop a persistent CardSelected when the cursor leaves. Safe for interrupted effects.
func stop() -> void:
    if is_instance_valid(card):
        if effect=="CardMaterialise":card.set_instance_shader_parameter("reveal",1.0)
        elif effect=="Dissolve":card.set_instance_shader_parameter("dissolve",0.0)
    complete()

func complete() -> void:
    if not active and is_queued_for_deletion():return
    active=false
    if effect=="CardSelected" and is_instance_valid(card):card.set_instance_shader_parameter("selected",0.0)
    finished.emit()
    queue_free()

func _exit_tree() -> void:
    if effect=="CardSelected" and is_instance_valid(card):card.set_instance_shader_parameter("selected",0.0)
