from pathlib import Path
import json,os
ROOT=Path(os.environ.get('KIT_REPO',str(Path(__file__).resolve().parents[3])))
entries=json.loads((ROOT/'assets/source/environment/manifest.json').read_text())
p=ROOT/'levels/review';p.mkdir(parents=True,exist_ok=True)
s=['[gd_scene load_steps='+str(len(entries)+7)+' format=3]']
for i,e in enumerate(entries):s.append(f'[ext_resource type="PackedScene" path="res://{e["path"]}" id="{i+1}"]')
s+=['''[sub_resource type="ProceduralSkyMaterial" id="sky_mat"]
sky_top_color = Color(0.38, 0.58, 0.77, 1)
sky_horizon_color = Color(0.87, 0.87, 0.8, 1)
[sub_resource type="Sky" id="sky"]
sky_material = SubResource("sky_mat")
[sub_resource type="Environment" id="env"]
background_mode = 2
sky = SubResource("sky")
ambient_light_source = 3
ambient_light_color = Color(0.85, 0.86, 0.9, 1)
ambient_light_energy = 0.35
reflected_light_source = 2
[sub_resource type="StandardMaterial3D" id="floor_mat"]
albedo_color = Color(0.19, 0.23, 0.28, 1)
roughness = 1.0
[sub_resource type="PlaneMesh" id="floor"]
material = SubResource("floor_mat")
size = Vector2(52, 54)
[node name="EnvironmentKit" type="Node3D"]
[node name="Environment" type="WorldEnvironment" parent="."]
environment = SubResource("env")
[node name="Sun" type="DirectionalLight3D" parent="."]
rotation_degrees = Vector3(-55, -25, 0)
light_energy = 0.7
shadow_enabled = true
[node name="Floor" type="MeshInstance3D" parent="."]
position = Vector3(22, -0.05, 22)
mesh = SubResource("floor")
[node name="Overview" type="Camera3D" parent="."]
position = Vector3(22, 48, 65)
rotation_degrees = Vector3(-48, 0, 0)
projection = 1
size = 54.0
far = 200.0
current = true
''']
for i,e in enumerate(entries):
 x=i%8*6;z=i//8*6
 s.append(f'[node name="{e["name"]}" parent="." instance=ExtResource("{i+1}")]\nposition = Vector3({x}, 0, {z})')
 label=e['name'].replace('kit_','').replace('prop_','').replace('_',' ')
 s.append(f'''[node name="Label_{i}" type="Label3D" parent="."]
position = Vector3({x+1}, 0.06, {z+3.4})
rotation_degrees = Vector3(-90, 0, 0)
text = "{label}"
font_size = 28
pixel_size = 0.012
outline_size = 5
modulate = Color(0.95, 0.95, 0.9, 1)
''')
(p/'EnvironmentKit.tscn').write_text('\n\n'.join(s).rstrip()+'\n')
