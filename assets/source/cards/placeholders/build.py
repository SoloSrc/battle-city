"""Original SOLOSRC symbolic placeholder art, MIT. No reference imagery used."""
from pathlib import Path
import json, math
ROOT=Path(__file__).resolve().parents[4]
OUT=ROOT/'assets/source/cards/placeholders'
def path(d,fill='currentColor',stroke='none',width=8): return f'<path d="{d}" fill="{fill}" stroke="{stroke}" stroke-width="{width}" stroke-linejoin="round" stroke-linecap="round"/>'
def circle(x,y,r,fill='currentColor'): return f'<circle cx="{x}" cy="{y}" r="{r}" fill="{fill}"/>'
def move(s,x=0,y=0,scale=1,angle=0): return f'<g transform="translate({x} {y}) rotate({angle}) scale({scale})">{s}</g>'
# Every shape is authored here, in local coordinates centered on the subject.
M={
'sword':path('M 0 -142 L 22 -95 L 12 45 L 48 52 L 48 68 L 12 70 L 12 125 L -12 125 L -12 70 L -48 68 L -48 52 L -12 45 L -22 -95 Z')+path('M 0 -100 L 0 40',stroke='#f8d8a0',width=4),
'shield':path('M -86 -100 L 0 -120 L 86 -100 L 75 32 Q 52 92 0 126 Q -52 92 -75 32 Z')+path('M -53 -72 L 0 -85 L 53 -72 L 45 20 Q 32 62 0 86 Q -32 62 -45 20 Z',fill='#f8d8a0'),
'bolt':path('M 24 -142 L -88 25 L -8 12 L -35 142 L 100 -38 L 18 -23 Z'),
'moon':path('M 40 -115 A 122 122 0 1 0 40 115 Q -105 0 40 -115 Z'),
'eye':path('M -135 0 Q 0 -140 135 0 Q 0 140 -135 0 Z')+circle(0,0,43,'#f8d8a0')+circle(0,0,22),
'wing':path('M -20 100 Q -130 20 -125 -108 L -82 -40 L -70 -136 L -37 -61 L -15 -143 L 4 -38 Q 60 1 20 65 Z'),
'helm':path('M -76 86 L -86 -20 Q -76 -101 0 -116 Q 76 -101 86 -20 L 76 86 L 23 104 L 20 7 L -20 7 L -23 104 Z')+path('M -55 -25 L 55 -25',stroke='#f8d8a0',width=12),
'hood':path('M -110 110 Q -90 -120 0 -140 Q 90 -120 110 110 Z')+path('M -58 3 Q 0 -90 58 3 L 30 60 L -30 60 Z',fill='#f8d8a0'),
'crown':path('M -110 -77 L -58 -16 L 0 -110 L 58 -16 L 110 -77 L 76 89 L -76 89 Z')+path('M -67 57 L 67 57',stroke='#f8d8a0',width=10),
'jar':path('M -72 -105 L 72 -105 L 55 -52 Q 129 -10 93 92 Q 0 134 -93 92 Q -129 -10 -55 -52 Z')+path('M -55 -45 Q 0 -28 55 -45 M -64 62 Q 0 83 64 62',stroke='#f8d8a0',width=10),
'beast':path('M -97 -35 L -113 -108 L -35 -78 L 0 -96 L 35 -78 L 113 -108 L 97 -35 L 105 42 L 40 111 L -40 111 L -105 42 Z')+path('M -64 -16 L -25 -6 M 64 -16 L 25 -6 M -23 48 L 0 69 L 23 48',stroke='#f8d8a0',width=10),
'skull':path('M -93 0 Q -118 -122 0 -130 Q 118 -122 93 0 L 51 40 L 51 105 L -51 105 L -51 40 Z')+circle(-40,-27,24,'#f8d8a0')+circle(40,-27,24,'#f8d8a0')+path('M -20 53 L -20 91 M 20 53 L 20 91',stroke='#f8d8a0',width=9),
'book':path('M -116 -96 Q -47 -110 0 -67 Q 47 -110 116 -96 L 116 102 Q 47 88 0 125 Q -47 88 -116 102 Z')+path('M 0 -56 L 0 99 M -87 -49 L -29 -27 M 87 -49 L 29 -27',stroke='#f8d8a0',width=10),
'hand':path('M -76 122 L -86 43 L -121 -10 Q -133 -35 -108 -39 L -66 -9 L -69 -83 Q -67 -110 -48 -92 L -25 -36 L -20 -116 Q -12 -139 4 -115 L 14 -38 L 34 -97 Q 50 -117 59 -92 L 48 -21 L 82 -58 Q 104 -67 101 -38 L 70 58 L 47 122 Z'),
'flame':path('M 3 -146 Q 45 -74 18 -38 Q 94 -77 109 -22 Q 145 112 0 133 Q -139 99 -98 1 Q -76 -39 -51 -65 Q -65 22 -19 31 Q -42 -81 3 -146 Z'),
'portal': '<ellipse rx="112" ry="69" fill="currentColor"/><ellipse rx="80" ry="43" fill="#f8d8a0"/><ellipse rx="59" ry="27" fill="currentColor"/>',
'wave':path('M -132 90 Q -29 96 -32 6 Q -34 -100 67 -107 Q 115 -102 133 -56 Q 27 -97 38 20 Q 43 106 132 90 L 132 127 L -132 127 Z'),
'star':path('M 0 -142 L 31 -39 L 133 -43 L 55 20 L 82 125 L 0 61 L -82 125 L -55 20 L -133 -43 L -31 -39 Z'),
'arrow':path('M -120 -32 L 33 -32 L 33 -80 L 124 0 L 33 80 L 33 32 L -120 32 Z'),
'leaf':path('M -94 102 Q -116 -84 117 -127 Q 132 73 -94 102 Z')+path('M -73 79 L 79 -76 M -10 13 L -27 -53 M 21 -19 L 71 -5',stroke='#f8d8a0',width=10),
'orb':circle(0,0,97)+path('M -105 -102 L -144 -135 M 105 -102 L 144 -135 M 0 -122 L 0 -166 M -122 0 L -165 0 M 122 0 L 165 0',stroke='currentColor',width=12)+circle(-25,-28,22,'#f8d8a0'),
'cross':path('M -23 -125 L 23 -125 L 23 -40 L 100 -40 L 100 5 L 23 5 L 23 125 L -23 125 L -23 5 L -100 5 L -100 -40 L -23 -40 Z'),
'axe':path('M -13 -130 L 13 -130 L 13 133 L -13 133 Z M 13 -93 Q 62 -36 109 -104 L 120 19 Q 63 -27 13 29 Z M -13 -93 Q -62 -36 -109 -104 L -120 19 Q -63 -27 -13 29 Z'),
'serpent':path('M -88 113 Q 114 137 74 35 Q 55 -10 -27 23 Q -129 62 -93 -51 Q -58 -131 56 -90 L 104 -56 L 49 -32 L 9 -58 Q -70 -70 -46 -14 Q 109 -61 130 63 Q 137 165 -88 113 Z'),
'crack':path('M -40 -143 L 48 -58 L -20 -5 L 63 52 L 12 143 L -8 54 L -90 7 L -5 -52 Z'),
'ring':'<circle r="101" fill="none" stroke="currentColor" stroke-width="30"/>',
'armor':path('M -54 -117 L 54 -117 L 65 -86 L 116 -66 L 80 4 L 58 -11 L 75 119 L -75 119 L -58 -11 L -80 4 L -116 -66 L -65 -86 Z')+path('M 0 -69 L 0 85 M -42 -45 L 0 -20 L 42 -45',stroke='#f8d8a0',width=10),
}
M.update({
'doll':circle(0,-96,33)+path('M -36 -51 L 36 -51 L 46 38 L -46 38 Z M -32 57 L -38 131 L -68 131 L -60 54 Z M 32 57 L 60 54 L 68 131 L 38 131 Z M -58 -43 L -104 8 L -124 -10 L -82 -62 Z M 58 -43 L 86 -103 L 109 -90 L 82 -23 Z')+circle(-48,46,11)+circle(48,46,11),
'elf':path('M -66 -44 L -118 -89 L -88 14 L -52 40 L -36 100 L 36 100 L 52 40 L 88 14 L 118 -89 L 66 -44 Q 0 -137 -66 -44 Z')+path('M -38 -5 L -12 4 M 38 -5 L 12 4',stroke='#f8d8a0',width=8),
'imp':path('M -61 -48 L -102 -137 L -16 -93 L 16 -93 L 102 -137 L 61 -48 Q 123 51 37 113 L -37 113 Q -123 51 -61 -48 Z')+path('M -47 -5 L -20 9 M 47 -5 L 20 9 M -42 46 Q 0 89 42 46',stroke='#f8d8a0',width=10),
'bat':path('M 0 -31 Q -74 -112 -145 -119 L -111 15 Q -82 -15 -59 39 Q -33 17 0 100 Q 33 17 59 39 Q 82 -15 111 15 L 145 -119 Q 74 -112 0 -31 Z'),
'giant':path('M -45 -129 L 45 -129 L 51 -62 L -51 -62 Z M -66 -49 L 66 -49 L 53 54 L -53 54 Z M -79 -42 L -125 -35 L -133 72 L -86 72 Z M 79 -42 L 125 -35 L 133 72 L 86 72 Z M -51 68 L -10 68 L -10 135 L -63 135 Z M 10 68 L 51 68 L 63 135 L 10 135 Z')+path('M -28 -92 L -9 -92 M 28 -92 L 9 -92',stroke='#f8d8a0',width=9),
'prayer':circle(0,-89,32)+path('M -32 -49 L -67 -12 L -104 122 L 104 122 L 67 -12 L 32 -49 L 17 -9 L 50 31 L 22 57 L 0 16 L -22 57 L -50 31 L -17 -9 Z'),
'claw':path('M -65 -110 L -44 9 L -103 94 L -43 63 L -19 30 L -8 125 L 17 60 L 22 16 L 95 94 L 75 33 L 48 -17 L 60 -110 L 25 -82 L 1 -26 L -21 -77 Z'),
})
# Main silhouette + secondary symbol: semantic, original emblems, not depictions
# of the licensed card illustrations. No text, stats or gameplay icons in art.
SPEC='''rogue_doll doll sword
celtic_guardian elf sword
harpie_lady wing claw
feral_imp imp moon
koumori_dragon serpent bat
giant_soldier_of_stone giant shield
mystical_elf prayer ring
airknight_parshath helm wing
archfiend_soldier helm flame
asura_priest crown ring
axe_of_despair axe moon
berserk_gorilla beast hand
black_luster_soldier_envoy_of_the_beginning armor star
blade_knight helm sword
book_of_moon book moon
bottomless_trap_hole portal arrow
breaker_the_magical_warrior sword orb
call_of_the_haunted cross wave
chaos_sorcerer hood orb
command_knight helm crown
creature_swap arrow beast
cyber_jar jar bolt
dark_balter_the_terrible skull flame
dd_assailant sword portal
dd_warrior_lady armor portal
delinquent_duo hood hood
don_zaloog hood sword
dust_tornado wave leaf
enemy_controller hand bolt
enraged_battle_ox beast axe
exiled_force shield arrow
fissure crack portal
gemini_elf hood leaf
giant_orc beast skull
goblin_attack_force sword sword
graceful_charity hand wing
gravekeepers_guard shield cross
gravekeepers_spy eye cross
heavy_storm wave wave
jinzo armor eye
kycoo_the_ghost_destroyer hood cross
lightning_vortex portal bolt
luster_dragon serpent wing
mad_dog_of_darkness beast moon
magician_of_faith hood star
marauding_captain helm arrow
metamorphosis leaf wing
mirror_force shield eye
mobius_the_frost_monarch crown wave
morphing_jar jar arrow
mystic_swordsman_lv2 sword leaf
mystic_tomato leaf eye
mystical_space_typhoon portal wave
nobleman_of_crossout sword cross
pot_of_greed jar hand
premature_burial cross hand
reaper_on_the_nightmare skull beast
reinforcement_of_the_army shield sword
ring_of_destruction ring flame
sakuretsu_armor armor crack
sangan beast eye
scapegoat beast wing
shining_angel wing star
sinister_serpent serpent wave
skilled_dark_magician book orb
smashing_ground hand crack
snatch_steal hand arrow
spirit_reaper skull moon
summoned_skull skull bolt
swords_of_revealing_light sword star
the_warrior_returning_alive helm cross
thousand_eyes_restrict eye ring
torrential_tribute wave cross
trap_hole portal crack
tribe_infecting_virus orb wave
tsukuyomi hood moon
waboku shield wing
widespread_ruin crack flame
zaborg_the_thunder_monarch crown bolt'''
spec={line.split()[0]:line.split()[1:] for line in SPEC.splitlines()}
palettes={'EARTH':('#c5ad80','#75644c','#322f2b'),'DARK':('#b39ab1','#725d7c','#302a40'),'LIGHT':('#e8ce8d','#b5945e','#534230'),'WATER':('#9bc3cb','#598c9d','#254656'),'WIND':('#acc8af','#638d81','#254943'),'FIRE':('#dfac87','#b46d54','#602f31'),'spell':('#a9c8b7','#648f80','#294a44'),'trap':('#d0acc0','#9a6d8e','#533348')}
manifest=[]
for p in sorted((ROOT/'data/cards').glob('*.json')):
    card=json.loads(p.read_text());id=card['id'];a,b=spec[id]
    bg,mid,ink=palettes[(card.get('monster') or {}).get('attribute',card['kind'])]
    # Shared framing leaves the outer 64 pixels expendable without establishing
    # a runtime crop policy. Every primary subject sits in the inner 360 square.
    motif=move(M[a],246,242,.87)+move(M[b],330,338,.39)
    svg=f'''<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512"><title>Original symbolic placeholder: {id}</title><rect width="512" height="512" fill="{bg}"/><path d="M 0 382 L 512 108 L 512 512 L 0 512 Z" fill="{mid}"/><circle cx="246" cy="238" r="171" fill="{bg}" stroke="{ink}" stroke-opacity=".16" stroke-width="3"/><path d="M 82 80 L 125 80 M 82 80 L 82 123 M 430 432 L 387 432 M 430 432 L 430 389" fill="none" stroke="{ink}" stroke-width="5"/><g color="{ink}">{motif}</g></svg>'''
    (OUT/(id+'.svg')).write_text(svg+'\n')
    manifest.append({'id':id,'name':card['name'],'kind':card['kind'],'motifs':[a,b],'source':f'assets/source/cards/placeholders/{id}.svg','texture':f'assets/cards/art/{id}.png','stage':'symbolic placeholder','license':'MIT'})
assert set(spec)=={m['id'] for m in manifest},'Roster mismatch'
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print(f'Authored {len(manifest)} original SVG placeholders')
