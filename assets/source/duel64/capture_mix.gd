extends SceneTree
var recorder: AudioEffectRecord
var scene: Node
var started: int
func _initialize() -> void:
    call_deferred("begin")
func begin() -> void:
    recorder=AudioEffectRecord.new()
    recorder.format=AudioStreamWAV.FORMAT_16_BITS
    AudioServer.add_bus_effect(0,recorder)
    recorder.set_recording_active(true)
    scene=load("res://tests/scenes/DuelStagingTest.tscn").instantiate()
    root.add_child(scene)
    started=Time.get_ticks_msec()
func _process(_delta:float) -> bool:
    if scene == null:return false
    var label=scene.get_node("Hud/Report")
    if str(label.text).begins_with("DuelStagingTest summary") or Time.get_ticks_msec()-started>90000:
        recorder.set_recording_active(false)
        var recording=recorder.get_recording()
        recording.save_to_wav("/tmp/duel64-runtime-mix.wav")
        print("MIX_CAPTURE_SECONDS ",recording.get_length())
        var passed := str(label.text).begins_with("DuelStagingTest summary")
        if not passed: push_error("Mix capture timed out before diagnostic summary")
        quit(0 if passed else 1)
    return false
