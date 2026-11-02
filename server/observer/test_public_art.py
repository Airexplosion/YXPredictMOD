import pathlib,sys,unittest
sys.path.insert(0,str(pathlib.Path(__file__).parent))
from export_public_art import select_sprite_index
from test_projection import number,blob

def config(*names):
    return b''.join(blob(2,number(1,21001+i)+blob(15,name.encode('utf8')))for i,name in enumerate(names))

class PublicArtTests(unittest.TestCase):
    def test_strategy_overrides_include_career_and_arbitrary_named_sprites(self):
        index={name:('bundle',i)for i,name in enumerate(('Card_0','Icon_Talent_1','Icon_Career_1','Special_Fate_Icon','Unused_Decoration'))}
        configs={'XianMoStrategyConfig':config('Icon_Career_1'),'FateStrategyConfig':config('Special_Fate_Icon')}
        selected=select_sprite_index(index,configs)
        self.assertEqual(set(selected),{'Card_0','Icon_Talent_1','Icon_Career_1','Special_Fate_Icon'})
        self.assertEqual(selected['Icon_Career_1'],index['Icon_Career_1'])

    def test_missing_required_override_fails_before_publishing_partial_assets(self):
        configs={'XianMoStrategyConfig':config('Icon_Career_1'),'FateStrategyConfig':config('Special_Fate_Icon')}
        with self.assertRaisesRegex(ValueError,'Missing native override sprites: Icon_Career_1, Special_Fate_Icon'):
            select_sprite_index({'Card_0':('bundle',1)},configs)

    def test_empty_overrides_keep_default_icons_without_exporting_unrelated_sprites(self):
        configs={'XianMoStrategyConfig':config(''),'FateStrategyConfig':config()}
        index={'Icon_XianMoStrategy_Skip':('bundle',1),'Icon_FateStrategy_21':('bundle',2),'Unused_Decoration':('bundle',3)}
        self.assertEqual(set(select_sprite_index(index,configs)),{'Icon_XianMoStrategy_Skip','Icon_FateStrategy_21'})

if __name__=='__main__':unittest.main()
