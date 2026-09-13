extends SceneTree
func _initialize() -> void:
    var manifest = JSON.parse_string(FileAccess.get_file_as_string("res://assets/source/cards/placeholders/manifest.json"))
    var count := 0
    for card in manifest:
        var texture = load("res://"+card.texture)
        if not texture is Texture2D or texture.get_size()!=Vector2(512,512):
            push_error("Invalid texture: "+card.id)
            quit(1)
            return
        var material := StandardMaterial3D.new()
        material.albedo_texture=texture
        if material.albedo_texture!=texture:
            quit(1)
            return
        count+=1
    print("CardPlaceholder PASS: %d Texture2D resources at 512x512, bound to StandardMaterial3D"%count)
    quit(0 if count==manifest.size() else 1)
