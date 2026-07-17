using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameLanguage
{
    English,
    SimplifiedChinese,
    Japanese
}

/// <summary>
/// Persistent localization service shared by every scene.  Text can be
/// translated either by attaching LocalizedText or by using one of the
/// registered UI labels below.
/// </summary>
public class GameLocalization : MonoBehaviour
{
    private const string PreferenceKey = "GameLanguage";
    private const string RuntimeObjectName = "Game Localization";
    private const GameLanguage DefaultLanguage = GameLanguage.SimplifiedChinese;
    private const string DirectEditorBattleSceneName = "草原";

    public static GameLocalization Instance { get; private set; }
    public static event Action<GameLanguage> LanguageChanged;
    public static bool LaunchedFromMainMenu { get; private set; }

    [SerializeField] private TMP_FontAsset cjkFont;
    [SerializeField] private TMP_FontAsset japaneseFont;

    private readonly Dictionary<string, string[]> translations = new Dictionary<string, string[]>
    {
        { "Start", new[] { "Start", "\u5f00\u59cb\u6e38\u620f", "\u30b2\u30fc\u30e0\u958b\u59cb" } },
        { "Setting", new[] { "Settings", "\u8bbe\u7f6e", "\u8a2d\u5b9a" } },
        { "Exit", new[] { "Exit", "\u9000\u51fa\u6e38\u620f", "\u7d42\u4e86" } },
        { "Music", new[] { "Music", "\u97f3\u4e50", "\u97f3\u697d" } },
        { "SFX", new[] { "Sound Effects", "\u97f3\u6548", "\u52b9\u679c\u97f3" } },
        { "FullScreen", new[] { "Full Screen", "\u5168\u5c4f", "\u30d5\u30eb\u30b9\u30af\u30ea\u30fc\u30f3" } },
        { "Save", new[] { "Save", "\u4fdd\u5b58", "\u4fdd\u5b58" } },
        { "T: Switch Player", new[] { "T: Switch Player", "T: \u5207\u6362\u89d2\u8272", "T: \u30ad\u30e3\u30e9\u30af\u30bf\u30fc\u5207\u66ff" } },
        { "K: Rune Panel", new[] { "K: Rune Panel", "K: \u7b26\u6587\u9762\u677f", "K: \u30eb\u30fc\u30f3\u30d1\u30cd\u30eb" } },
        { "I: Character Panel", new[] { "I: Character Panel", "I: \u89d2\u8272\u9762\u677f", "I: \u30ad\u30e3\u30e9\u30af\u30bf\u30fc\u30d1\u30cd\u30eb" } },
        { "Rune Panel", new[] { "Rune Panel", "\u7b26\u6587\u9762\u677f", "\u30eb\u30fc\u30f3\u30d1\u30cd\u30eb" } },
        { "Rune Bag", new[] { "Rune Bag", "\u7b26\u6587\u80cc\u5305", "\u30eb\u30fc\u30f3\u30d0\u30c3\u30b0" } },
        { "Rune Skill Panel", new[] { "Rune Skill Panel", "\u7b26\u6587\u6280\u80fd\u9762\u677f", "\u30eb\u30fc\u30f3\u30b9\u30ad\u30eb\u30d1\u30cd\u30eb" } },
        { "Description", new[] { "Description", "\u8bf4\u660e", "\u8aac\u660e" } },
        { "Description: -", new[] { "Description: -", "\u8bf4\u660e: -", "\u8aac\u660e: -" } },
        { "Effect: -", new[] { "Effect: -", "\u6548\u679c: -", "\u52b9\u679c: -" } },
        { "Type: -", new[] { "Type: -", "\u7c7b\u578b: -", "\u7a2e\u5225: -" } },
        { "Rune Name: None", new[] { "Rune Name: None", "\u7b26\u6587\u540d\u79f0: \u65e0", "\u30eb\u30fc\u30f3\u540d: \u306a\u3057" } },
        { "Selected Rune: None", new[] { "Selected Rune: None", "\u5df2\u9009\u7b26\u6587: \u65e0", "\u9078\u629e\u4e2d\u306e\u30eb\u30fc\u30f3: \u306a\u3057" } },
        { "No rune", new[] { "No rune", "\u65e0\u7b26\u6587", "\u30eb\u30fc\u30f3\u306a\u3057" } },
        { "Empty", new[] { "Empty", "\u7a7a", "\u7a7a\u304d" } },
        { "Hover a skill or rune to view details.", new[] { "Hover a skill or rune to view details.", "\u60ac\u505c\u5728\u6280\u80fd\u6216\u7b26\u6587\u4e0a\u67e5\u770b\u8be6\u60c5\u3002", "\u30b9\u30ad\u30eb\u307e\u305f\u306f\u30eb\u30fc\u30f3\u306b\u30de\u30a6\u30b9\u3092\u5408\u308f\u305b\u3066\u8a73\u7d30\u3092\u8868\u793a\u3002" } },
        { "Player Attributes", new[] { "Player Attributes", "\u89d2\u8272\u5c5e\u6027", "\u30d7\u30ec\u30a4\u30e4\u30fc\u80fd\u529b" } },
        { "Player", new[] { "Player", "\u89d2\u8272", "\u30d7\u30ec\u30a4\u30e4\u30fc" } },
        { "Buff / Rune / Skill Info Reserved", new[] { "Buff / Rune / Skill Info Reserved", "\u589e\u76ca / \u7b26\u6587 / \u6280\u80fd\u4fe1\u606f\u9884\u7559", "\u30d0\u30d5 / \u30eb\u30fc\u30f3 / \u30b9\u30ad\u30eb\u60c5\u5831\u4e88\u7d04" } },
        { "Heal +10", new[] { "Heal +10", "\u6cbb\u7597 +10", "\u56de\u5fa9 +10" } },
        { "Loading 0%", new[] { "Loading 0%", "\u52a0\u8f7d\u4e2d 0%", "\u30ed\u30fc\u30c9\u4e2d 0%" } },
        { "Restart", new[] { "Restart", "\u91cd\u65b0\u5f00\u59cb", "\u30ea\u30b9\u30bf\u30fc\u30c8" } },
        { "Main Menu", new[] { "Main Menu", "\u4e3b\u83dc\u5355", "\u30e1\u30a4\u30f3\u30e1\u30cb\u30e5\u30fc" } },
        { "DEFEAT", new[] { "DEFEAT", "\u5931\u8d25", "\u6557\u5317" } },
        { "DEFEAT!", new[] { "DEFEAT!", "\u5931\u8d25!", "\u6557\u5317!" } },
        { "you win!", new[] { "you win!", "\u4f60\u8d62\u4e86!", "\u52dd\u5229!" } },
        { "Victory", new[] { "Victory", "\u80dc\u5229", "\u52dd\u5229" } }
        ,{ "Life Rune", new[] { "Life Rune", "\u751f\u547d\u7b26\u6587", "\u751f\u547d\u30eb\u30fc\u30f3" } }
        ,{ "Shield Rune", new[] { "Shield Rune", "\u62a4\u76fe\u7b26\u6587", "\u30b7\u30fc\u30eb\u30c9\u30eb\u30fc\u30f3" } }
        ,{ "Mana Rune", new[] { "Mana Rune", "\u9b54\u529b\u7b26\u6587", "\u30de\u30ca\u30eb\u30fc\u30f3" } }
        ,{ "Thorn Rune", new[] { "Thorn Rune", "\u8346\u68d8\u7b26\u6587", "\u30bd\u30fc\u30f3\u30eb\u30fc\u30f3" } }
        ,{ "Luck Rune", new[] { "Luck Rune", "\u5e78\u8fd0\u7b26\u6587", "\u5e78\u904b\u30eb\u30fc\u30f3" } }
        ,{ "rune.life.name", new[] { "Life Rune", "\u751f\u547d\u7b26\u6587", "\u751f\u547d\u30eb\u30fc\u30f3" } }
        ,{ "rune.life.flavor", new[] { "A rune born from life force. It turns vitality into healing, damage, shields, and growth.", "\u4ee5\u751f\u547d\u4e3a\u6e90\u6cc9\u7684\u7b26\u6587\uff0c\u80fd\u591f\u5f3a\u5316\u4f53\u9b44\uff0c\u5e76\u5c06\u751f\u547d\u529b\u8f6c\u5316\u4e3a\u56de\u590d\u3001\u4f24\u5bb3\u4e0e\u6210\u957f\u3002", "\u751f\u547d\u529b\u3092\u6e90\u3068\u3059\u308b\u30eb\u30fc\u30f3\u3002\u4f53\u529b\u3092\u5f37\u5316\u3057\u3001\u56de\u5fa9\u30fb\u30c0\u30e1\u30fc\u30b8\u30fb\u30b7\u30fc\u30eb\u30c9\u30fb\u6210\u9577\u3078\u5909\u3048\u308b\u3002" } }
        ,{ "rune.life.full_description", new[] { "縲審une Effects縲曾n1 Piece: Kills drop 1 Growth Soul and 1 Life Soul.\n2 Pieces: Casting a skill heals 5% max HP.\n3 Pieces: After casting, your next enemy hit deals bonus damage equal to 5% max HP.\n4 Pieces: Life Rune overheal becomes shield, capped at 100% max HP.\n5 Pieces: Max HP +50%.\n\n縲心et Bonuses縲曾n2 Pieces - Life Affinity: Healing received +15%.\n4 Pieces - Life Resonance: At 50% HP or higher, damage dealt +20%. Below 50% HP, monster damage taken -25%. Healing from below 50% to 50% or higher grants both effects for 8s; refreshing does not stack.\n5 Pieces - Life Dominion: Every 100 max HP grants +1 Physical Attack, Physical Defense, Special Attack, Special Defense, and Speed. Does not increase Max HP or Luck.", "\u3010\u7b26\u6587\u672c\u4f53\u6548\u679c\u3011\n1\u4ef6\uff1a\u51fb\u6740\u654c\u4eba\u65f6\uff0c\u989d\u5916\u6389\u843d1\u4e2a\u6210\u957f\u4e4b\u9b42\u548c1\u4e2a\u751f\u547d\u4e4b\u9b42\u3002\n2\u4ef6\uff1a\u6bcf\u6b21\u65bd\u653e\u6280\u80fd\u65f6\uff0c\u6062\u590d\u81ea\u8eab5%\u6700\u5927\u751f\u547d\u503c\u3002\n3\u4ef6\uff1a\u6bcf\u6b21\u65bd\u653e\u6280\u80fd\u540e\uff0c\u4e0b\u4e00\u6b21\u5bf9\u654c\u4eba\u9020\u6210\u4f24\u5bb3\u65f6\uff0c\u8ffd\u52a0\u76f8\u5f53\u4e8e\u81ea\u8eab5%\u6700\u5927\u751f\u547d\u503c\u7684\u4f24\u5bb3\u3002\n4\u4ef6\uff1a\u751f\u547d\u7b26\u6587\u4ea7\u751f\u7684\u6cbb\u7597\u6ea2\u51fa\u65f6\uff0c\u5c06\u6ea2\u51fa\u6cbb\u7597\u91cf\u8f6c\u5316\u4e3a\u62a4\u76fe\uff0c\u6700\u591a\u81f3\u81ea\u8eab100%\u6700\u5927\u751f\u547d\u503c\u3002\n5\u4ef6\uff1a\u81ea\u8eab\u6700\u5927\u751f\u547d\u503c\u63d0\u9ad850%\u3002\n\n\u3010\u5957\u88c5\u989d\u5916\u6548\u679c\u3011\n2\u4ef6\u5957\u00b7\u751f\u547d\u4eb2\u548c\uff1a\u81ea\u8eab\u53d7\u5230\u7684\u6cbb\u7597\u6548\u679c\u63d0\u9ad815%\u3002\n4\u4ef6\u5957\u00b7\u751f\u547d\u5171\u9e23\uff1a\u5f53\u524d\u751f\u547d\u503c\u9ad8\u4e8e\u6216\u7b49\u4e8e50%\u65f6\uff0c\u9020\u6210\u7684\u4f24\u5bb3\u63d0\u9ad820%\uff1b\u4f4e\u4e8e50%\u65f6\uff0c\u53d7\u5230\u602a\u7269\u9020\u6210\u7684\u4f24\u5bb3\u964d\u4f4e25%\u3002\u751f\u547d\u503c\u4ece50%\u4ee5\u4e0b\u6062\u590d\u81f350%\u4ee5\u4e0a\u65f6\uff0c\u83b7\u5f978\u79d2\u751f\u547d\u5171\u9e23\uff0c\u540c\u65f6\u83b7\u5f97\u4e0a\u8ff0\u589e\u4f24\u548c\u51cf\u4f24\uff0c\u5237\u65b0\u4f46\u4e0d\u53e0\u52a0\u3002\n5\u4ef6\u5957\u00b7\u751f\u547d\u7edf\u5fa1\uff1a\u6bcf\u62e5\u6709100\u70b9\u6700\u5927\u751f\u547d\u503c\uff0c\u7269\u7406\u653b\u51fb\u3001\u7269\u7406\u9632\u5fa1\u3001\u7279\u6b8a\u653b\u51fb\u3001\u7279\u6b8a\u9632\u5fa1\u548c\u901f\u5ea6\u5404\u63d0\u9ad81\u70b9\uff0c\u4e0d\u63d0\u9ad8\u6700\u5927\u751f\u547d\u503c\u4e0e\u5e78\u8fd0\u3002", "\u3010\u30eb\u30fc\u30f3\u672c\u4f53\u3011\n1\u500b\uff1a\u6575\u3092\u5012\u3059\u3068\u6210\u9577\u30bd\u30a6\u30eb\u30681\u500b\u306e\u751f\u547d\u30bd\u30a6\u30eb\u3092\u8ffd\u52a0\u30c9\u30ed\u30c3\u30d7\u3002\n2\u500b\uff1a\u30b9\u30ad\u30eb\u767a\u52d5\u6642\u3001\u6700\u5927HP\u306e5%\u3092\u56de\u5fa9\u3002\n3\u500b\uff1a\u30b9\u30ad\u30eb\u5f8c\u3001\u6b21\u306e\u5bfe\u654c\u30d2\u30c3\u30c8\u306b\u6700\u5927HP\u306e5%\u5206\u306e\u8ffd\u52a0\u30c0\u30e1\u30fc\u30b8\u3002\n4\u500b\uff1a\u751f\u547d\u30eb\u30fc\u30f3\u306e\u904e\u5270\u56de\u5fa9\u3092\u30b7\u30fc\u30eb\u30c9\u306b\u5909\u63db\u3002\u4e0a\u9650\u306f\u6700\u5927HP\u306e100%\u3002\n5\u500b\uff1a\u6700\u5927HP+50%\u3002\n\n\u3010\u30bb\u30c3\u30c8\u8ffd\u52a0\u52b9\u679c\u3011\n2\u500b\u30fb\u751f\u547d\u89aa\u548c\uff1a\u53d7\u3051\u308b\u56de\u5fa9\u91cf+15%\u3002\n4\u500b\u30fb\u751f\u547d\u5171\u9cf4\uff1aHP50%\u4ee5\u4e0a\u3067\u4e0e\u30c0\u30e1\u30fc\u30b8+20%\u300150%\u672a\u6e80\u3067\u30e2\u30f3\u30b9\u30bf\u30fc\u304b\u3089\u306e\u30c0\u30e1\u30fc\u30b8-25%\u3002HP50%\u672a\u6e80\u304b\u308950%\u4ee5\u4e0a\u3078\u56de\u5fa9\u3059\u308b\u30688\u79d2\u9593\u4e21\u65b9\u3092\u5f97\u308b\u3002\u66f4\u65b0\u3055\u308c\u308b\u304c\u91cd\u8907\u3057\u306a\u3044\u3002\n5\u500b\u30fb\u751f\u547d\u7d71\u5fa1\uff1a\u6700\u5927HP100\u3054\u3068\u306b\u7269\u7406\u653b\u6483\u3001\u7269\u7406\u9632\u5fa1\u3001\u7279\u6b8a\u653b\u6483\u3001\u7279\u6b8a\u9632\u5fa1\u3001\u901f\u5ea6+1\u3002\u6700\u5927HP\u3068\u904b\u306f\u5897\u3048\u306a\u3044\u3002" } }
        ,{ "rune.shield.name", new[] { "Shield Rune", "\u62a4\u76fe\u7b26\u6587", "\u30b7\u30fc\u30eb\u30c9\u30eb\u30fc\u30f3" } }
        ,{ "rune.shield.flavor", new[] { "A rune of protection that turns shield power into defense and offense.", "\u5b88\u62a4\u610f\u5fd7\u51dd\u7ed3\u800c\u6210\u7684\u7b26\u6587\uff0c\u80fd\u5c06\u62a4\u76fe\u8f6c\u5316\u4e3a\u66f4\u5f3a\u7684\u9632\u5fa1\u4e0e\u653b\u51fb\u529b\u91cf\u3002", "\u5b88\u308a\u306e\u610f\u5fd7\u304c\u7d50\u6676\u5316\u3057\u305f\u30eb\u30fc\u30f3\u3002\u30b7\u30fc\u30eb\u30c9\u3092\u9632\u5fa1\u3068\u653b\u6483\u306e\u529b\u306b\u5909\u3048\u308b\u3002" } }
        ,{ "rune.shield.full_description", new[] { "縲審une Effects縲曾n1 Piece: After 3s without monster damage, refill shield up to 50% max HP.\n2 Pieces: While shielded, damage dealt +30%.\n3 Pieces: Kills drop 1 Function Soul and 1 Shield Soul.\n4 Pieces: Shield gained +100%, affected by Shield Efficiency.\n5 Pieces: After casting, your next enemy hit deals bonus damage equal to 15% current shield, affected by Shield Efficiency.\n\n縲心et Bonuses縲曾n2 Pieces - Solid Barrier: While shielded, shield damage taken -15%. This only affects the shield portion.\n4 Pieces - Barrier Reconstruction: When shield breaks, gain 3s of 40% monster damage reduction and immunity to knockback/hit-stun. When it ends, gain shield equal to 30% max HP. 15s cooldown.\n5 Pieces - Unfallen Fortress: Shield cap becomes 300% max HP. Elite/Boss kills grant permanent Shield Efficiency +10%, up to 300%.", "\u3010\u7b26\u6587\u672c\u4f53\u6548\u679c\u3011\n1\u4ef6\uff1a\u8fde\u7eed3\u79d2\u672a\u53d7\u5230\u602a\u7269\u4f24\u5bb3\u540e\uff0c\u81ea\u52a8\u6062\u590d\u62a4\u76fe\uff0c\u76f4\u5230\u8fbe\u523050%\u6700\u5927\u751f\u547d\u503c\u3002\n2\u4ef6\uff1a\u81ea\u8eab\u62e5\u6709\u62a4\u76fe\u65f6\uff0c\u9020\u6210\u7684\u4f24\u5bb3\u63d0\u9ad830%\u3002\n3\u4ef6\uff1a\u51fb\u6740\u654c\u4eba\u65f6\uff0c\u989d\u5916\u6389\u843d1\u4e2a\u529f\u80fd\u4e4b\u9b42\u548c1\u4e2a\u62a4\u76fe\u4e4b\u9b42\u3002\n4\u4ef6\uff1a\u81ea\u8eab\u83b7\u5f97\u7684\u62a4\u76fe\u91cf\u63d0\u9ad8100%\uff0c\u53d7\u62a4\u76fe\u6548\u7387\u5f71\u54cd\u3002\n5\u4ef6\uff1a\u6bcf\u6b21\u65bd\u653e\u6280\u80fd\u540e\uff0c\u4e0b\u4e00\u6b21\u5bf9\u654c\u4eba\u9020\u6210\u4f24\u5bb3\u65f6\uff0c\u8ffd\u52a0\u76f8\u5f53\u4e8e\u81ea\u8eab\u5f53\u524d\u62a4\u76fe\u503c15%\u7684\u4f24\u5bb3\uff0c\u53d7\u62a4\u76fe\u6548\u7387\u5f71\u54cd\u3002\n\n\u3010\u5957\u88c5\u989d\u5916\u6548\u679c\u3011\n2\u4ef6\u5957\u00b7\u575a\u56fa\u5c4f\u969c\uff1a\u81ea\u8eab\u62e5\u6709\u62a4\u76fe\u65f6\uff0c\u62a4\u76fe\u53d7\u5230\u7684\u4f24\u5bb3\u964d\u4f4e15%\uff0c\u53ea\u5f71\u54cd\u62a4\u76fe\u627f\u53d7\u7684\u90e8\u5206\u3002\n4\u4ef6\u5957\u00b7\u58c1\u5792\u91cd\u6784\uff1a\u62a4\u76fe\u88ab\u51fb\u7834\u65f6\uff0c\u83b7\u5f973\u79d2\u58c1\u5792\u91cd\u6784\uff1a\u53d7\u5230\u602a\u7269\u4f24\u5bb3\u964d\u4f4e40%\uff0c\u514d\u75ab\u51fb\u9000\u548c\u786c\u76f4\u3002\u7ed3\u675f\u65f6\u83b7\u5f9730%\u6700\u5927\u751f\u547d\u503c\u62a4\u76fe\u300215\u79d2\u51b7\u5374\u3002\n5\u4ef6\u5957\u00b7\u4e0d\u843d\u8981\u585e\uff1a\u62a4\u76fe\u4e0a\u9650\u63d0\u9ad8\u81f3300%\u6700\u5927\u751f\u547d\u503c\u3002\u51fb\u6740\u7cbe\u82f1\u6216Boss\u65f6\uff0c\u62a4\u76fe\u6548\u7387\u6c38\u4e45+10%\uff0c\u6700\u9ad8300%\u3002", "\u3010\u30eb\u30fc\u30f3\u672c\u4f53\u3011\n1\u500b\uff1a3\u79d2\u9593\u30e2\u30f3\u30b9\u30bf\u30fc\u30c0\u30e1\u30fc\u30b8\u3092\u53d7\u3051\u306a\u3044\u3068\u3001\u6700\u5927HP50%\u307e\u3067\u30b7\u30fc\u30eb\u30c9\u56de\u5fa9\u3002\n2\u500b\uff1a\u30b7\u30fc\u30eb\u30c9\u4e2d\u3001\u4e0e\u30c0\u30e1\u30fc\u30b8+30%\u3002\n3\u500b\uff1a\u6575\u3092\u5012\u3059\u3068\u6a5f\u80fd\u30bd\u30a6\u30eb\u3068\u30b7\u30fc\u30eb\u30c9\u30bd\u30a6\u30eb\u30921\u500b\u305a\u3064\u8ffd\u52a0\u30c9\u30ed\u30c3\u30d7\u3002\n4\u500b\uff1a\u83b7\u5f97\u30b7\u30fc\u30eb\u30c9+100%\u3002\u30b7\u30fc\u30eb\u30c9\u52b9\u7387\u306e\u5f71\u97ff\u3092\u53d7\u3051\u308b\u3002\n5\u500b\uff1a\u30b9\u30ad\u30eb\u5f8c\u306e\u6b21\u306e\u5bfe\u654c\u30d2\u30c3\u30c8\u306b\u73fe\u5728\u30b7\u30fc\u30eb\u30c915%\u5206\u306e\u8ffd\u52a0\u30c0\u30e1\u30fc\u30b8\u3002\n\n\u3010\u30bb\u30c3\u30c8\u8ffd\u52a0\u52b9\u679c\u3011\n2\u500b\u30fb\u5805\u56fa\u306a\u7d50\u754c\uff1a\u30b7\u30fc\u30eb\u30c9\u304c\u53d7\u3051\u308b\u30c0\u30e1\u30fc\u30b8-15%\u3002\n4\u500b\u30fb\u58c1\u306e\u518d\u69cb\u7bc9\uff1a\u30b7\u30fc\u30eb\u30c9\u7834\u58ca\u6642\u30013\u79d2\u9593\u30e2\u30f3\u30b9\u30bf\u30fc\u30c0\u30e1\u30fc\u30b8-40%\u3001\u30ce\u30c3\u30af\u30d0\u30c3\u30af\u3068\u3072\u308b\u307f\u7121\u52b9\u3002\u7d42\u4e86\u6642\u306b\u6700\u5927HP30%\u306e\u30b7\u30fc\u30eb\u30c9\u300215\u79d2CD\u3002\n5\u500b\u30fb\u4e0d\u843d\u306e\u8981\u585e\uff1a\u30b7\u30fc\u30eb\u30c9\u4e0a\u9650\u304c\u6700\u5927HP300%\u306b\u306a\u308b\u3002\u30a8\u30ea\u30fc\u30c8/Boss\u6483\u7834\u3067\u30b7\u30fc\u30eb\u30c9\u52b9\u7387+10%\uff08\u6700\u5927300%\uff09\u3002" } }
        ,{ "rune.mana.name", new[] { "Mana Rune", "\u9b54\u529b\u7b26\u6587", "\u30de\u30ca\u30eb\u30fc\u30f3" } }
        ,{ "rune.mana.flavor", new[] { "A pure mana rune that expands mana and converts overflow into power.", "\u7531\u7eaf\u7cb9\u9b54\u529b\u51dd\u7ed3\u800c\u6210\u7684\u7b26\u6587\uff0c\u80fd\u6269\u5f20\u6cd5\u529b\u5bb9\u91cf\uff0c\u5c06\u6ea2\u51fa\u7684\u9b54\u529b\u8f6c\u5316\u4e3a\u7206\u53d1\u3002", "\u7d14\u7c8b\u306a\u30de\u30ca\u304b\u3089\u751f\u307e\u308c\u305f\u30eb\u30fc\u30f3\u3002\u30de\u30ca\u3068\u6ea2\u308c\u305f\u529b\u3092\u7206\u767a\u529b\u306b\u5909\u3048\u308b\u3002" } }
        ,{ "rune.mana.full_description", new[] { "縲審une Effects縲曾n1 Piece: Max Mana +200.\n2 Pieces: Mana regeneration +150%.\n3 Pieces: Kills drop 1 Energy Soul and 1 Mana Soul. Mana recovery overflow becomes Mana Overflow, capped at 200% max mana.\n4 Pieces: Casting a skill consumes up to 20% max mana as extra mana, current mana first then Mana Overflow, to strengthen that skill's configured Mana Rune bonus.\n5 Pieces: Every 100 max mana grants +1 Physical Attack, Physical Defense, Special Attack, Special Defense, and Speed.\n\n縲心et Bonuses縲曾n2 Pieces - Mana Flow: After a successful cast, refund 15% of the skill's base mana cost.\n4 Pieces - Arcane Resonance: When Mana Rune extra spending totals 20% max mana, gain 8s of +25% damage, +25% cooldown recovery speed, and +75% Mana Rune bonus contribution. The counter resets on trigger.\n5 Pieces - Arcane Overload: Mana Soul recovery becomes 400%, Mana Overflow cap becomes 300% max mana, and Elite/Boss kills grant permanent Mana Conversion Efficiency +10%, up to 300%.", "\u3010\u7b26\u6587\u672c\u4f53\u6548\u679c\u3011\n1\u4ef6\uff1a\u6700\u5927\u9b54\u529b\u503c\u63d0\u9ad8200\u70b9\u3002\n2\u4ef6\uff1a\u9b54\u529b\u6062\u590d\u901f\u5ea6\u63d0\u9ad8150%\u3002\n3\u4ef6\uff1a\u51fb\u6740\u654c\u4eba\u65f6\uff0c\u989d\u5916\u6389\u843d1\u4e2a\u80fd\u91cf\u4e4b\u9b42\u548c1\u4e2a\u9b54\u529b\u4e4b\u9b42\u3002\u83b7\u5f97\u9b54\u529b\u65f6\uff0c\u8d85\u8fc7\u6700\u5927\u9b54\u529b\u503c\u7684\u90e8\u5206\u8f6c\u5316\u4e3aMana Overflow\uff0c\u6700\u591a\u81f3200%\u6700\u5927\u9b54\u529b\u503c\u3002\n4\u4ef6\uff1a\u65bd\u653e\u6280\u80fd\u65f6\uff0c\u989d\u5916\u6d88\u8017\u6700\u591a20%\u6700\u5927\u9b54\u529b\u503c\u7684\u9b54\u529b\uff0c\u4f18\u5148\u4f7f\u7528\u5f53\u524d\u9b54\u529b\uff0c\u4e0d\u8db3\u65f6\u518d\u4f7f\u7528Mana Overflow\uff0c\u5f3a\u5316\u8be5\u6280\u80fd\u7684\u7b26\u6587\u5956\u52b1\u3002\n5\u4ef6\uff1a\u6bcf\u62e5\u6709100\u70b9\u6700\u5927\u9b54\u529b\u503c\uff0c\u7269\u7406\u653b\u51fb\u3001\u7269\u7406\u9632\u5fa1\u3001\u7279\u6b8a\u653b\u51fb\u3001\u7279\u6b8a\u9632\u5fa1\u548c\u901f\u5ea6\u5404+1\u3002\n\n\u3010\u5957\u88c5\u989d\u5916\u6548\u679c\u3011\n2\u4ef6\u5957\u00b7\u9b54\u529b\u56de\u6d41\uff1a\u6210\u529f\u65bd\u653e\u6280\u80fd\u540e\uff0c\u8fd4\u8fd8\u8be5\u6280\u80fd\u57fa\u7840\u9b54\u529b\u6d88\u8017\u768415%\u3002\n4\u4ef6\u5957\u00b7\u5965\u672f\u5171\u9e23\uff1a\u901a\u8fc7\u9b54\u529b\u7b26\u6587\u989d\u5916\u8017\u84dd\u7d2f\u8ba1\u8fbe\u523020%\u6700\u5927\u9b54\u529b\u503c\u65f6\uff0c\u83b7\u5f978\u79d2\u5965\u672f\u5171\u9e23\uff1a\u9020\u6210\u4f24\u5bb3+25%\uff0c\u6280\u80fd\u51b7\u5374\u6062\u590d\u901f\u5ea6+25%\uff0cMana Rune\u989d\u5916\u8017\u84dd\u4ea7\u751f\u7684\u6280\u80fd\u5f3a\u5316\u6548\u679c+75%\u3002\u89e6\u53d1\u540e\u7d2f\u8ba1\u503c\u6e05\u96f6\u3002\n5\u4ef6\u5957\u00b7\u5965\u672f\u8d85\u8f7d\uff1a\u9b54\u529b\u4e4b\u9b42\u6062\u590d\u91cf\u63d0\u9ad8\u81f3400%\uff0cMana Overflow\u4e0a\u9650\u63d0\u9ad8\u81f3300%\u6700\u5927\u9b54\u529b\u503c\u3002\u51fb\u6740\u7cbe\u82f1\u6216Boss\u65f6\uff0c\u9b54\u529b\u8f6c\u5316\u6548\u7387\u6c38\u4e45+10%\uff0c\u6700\u9ad8300%\u3002", "\u3010\u30eb\u30fc\u30f3\u672c\u4f53\u3011\n1\u500b\uff1a\u6700\u5927MP+200\u3002\n2\u500b\uff1aMP\u56de\u5fa9\u901f\u5ea6+150%\u3002\n3\u500b\uff1a\u6575\u3092\u5012\u3059\u3068\u30a8\u30cd\u30eb\u30ae\u30fc\u30bd\u30a6\u30eb\u3068\u30de\u30ca\u30bd\u30a6\u30eb\u30921\u500b\u305a\u3064\u8ffd\u52a0\u30c9\u30ed\u30c3\u30d7\u3002\u904e\u5270MP\u56de\u5fa9\u306fMana Overflow\u306b\u306a\u308a\u3001\u6700\u5927MP200%\u307e\u3067\u84c4\u7a4d\u3002\n4\u500b\uff1a\u30b9\u30ad\u30eb\u6642\u3001\u6700\u5927MP20%\u307e\u3067\u8ffd\u52a0\u6d88\u8cbb\u3057\u3001\u30de\u30ca\u30eb\u30fc\u30f3\u5f37\u5316\u3092\u5897\u5e45\u3002\u73fe\u5728MP\u3092\u5148\u306b\u4f7f\u3044\u3001\u4e0d\u8db3\u5206\u3092Mana Overflow\u304b\u3089\u4f7f\u3046\u3002\n5\u500b\uff1a\u6700\u5927MP100\u3054\u3068\u306b\u7269\u7406\u653b\u6483\u3001\u7269\u7406\u9632\u5fa1\u3001\u7279\u6b8a\u653b\u6483\u3001\u7279\u6b8a\u9632\u5fa1\u3001\u901f\u5ea6+1\u3002\n\n\u3010\u30bb\u30c3\u30c8\u8ffd\u52a0\u52b9\u679c\u3011\n2\u500b\u30fb\u30de\u30ca\u9084\u6d41\uff1a\u30b9\u30ad\u30eb\u6210\u529f\u5f8c\u3001\u57fa\u672cMP\u6d88\u8cbb\u306e15%\u3092\u8fd4\u9084\u3002\n4\u500b\u30fb\u5965\u8853\u5171\u9cf4\uff1a\u8ffd\u52a0MP\u6d88\u8cbb\u304c\u6700\u5927MP20%\u306b\u9054\u3059\u308b\u30688\u79d2\u9593\u3001\u4e0e\u30c0\u30e1\u30fc\u30b8+25%\u3001CD\u56de\u5fa9\u901f\u5ea6+25%\u3001\u30de\u30ca\u30eb\u30fc\u30f3\u5f37\u5316\u8ca2\u732e+75%\u3002\n5\u500b\u30fb\u5965\u8853\u8d85\u8f09\uff1a\u30de\u30ca\u30bd\u30a6\u30eb\u56de\u5fa9\u91cf400%\u3001Mana Overflow\u4e0a\u9650\u6700\u5927MP300%\u3002\u30a8\u30ea\u30fc\u30c8/Boss\u6483\u7834\u3067\u30de\u30ca\u5909\u63db\u52b9\u7387+10%\uff08\u6700\u5927300%\uff09\u3002" } }
        ,{ "rune.thorn.name", new[] { "Thorn Rune", "\u8346\u68d8\u7b26\u6587", "\u30bd\u30fc\u30f3\u30eb\u30fc\u30f3" } }
        ,{ "rune.thorn.flavor", new[] { "A rune of pain and retaliation that turns incoming attacks into thorn damage.", "\u7531\u75db\u82e6\u4e0e\u53cd\u51fb\u610f\u5fd7\u51dd\u7ed3\u800c\u6210\u7684\u7b26\u6587\uff0c\u80fd\u5c06\u627f\u53d7\u7684\u653b\u51fb\u8f6c\u5316\u4e3a\u53cd\u51fb\u529b\u91cf\u3002", "\u75db\u307f\u3068\u53cd\u6483\u306e\u610f\u5fd7\u304c\u7d50\u6676\u5316\u3057\u305f\u30eb\u30fc\u30f3\u3002\u53d7\u3051\u305f\u653b\u6483\u3092\u68d8\u306e\u53cd\u6483\u306b\u5909\u3048\u308b\u3002" } }
        ,{ "rune.thorn.full_description", new[] { "縲審une Effects縲曾n1 Piece: Monster damage taken -25%.\n2 Pieces: When hit by a monster, deal Thorn damage to the attacker. Base Thorn value is (10% max HP + main attributes + Luck) x 30%.\n3 Pieces: Thorn damage +100%.\n4 Pieces: After casting, your next enemy hit adds Thorn damage equal to 150% current Thorn value.\n5 Pieces: When hit by a monster, auto-release Thorn Counter around you.\n\n縲心et Bonuses縲曾n2 Pieces - Thorn Drain: Thorn damage that successfully hits restores 2% max HP, at most once per second.\n4 Pieces - Thorn Backlash: When taking monster damage, this hit is reduced by an extra 30% and the attacker takes Thorn damage equal to 200% current Thorn value. 5s cooldown.\n5 Pieces - Thousand-Thorn Counter: Thorn damage +100%, Thorn Counter cooldown becomes 2s, and Elite/Boss kills grant permanent Thorn Efficiency +10%, up to 300%.", "\u3010\u7b26\u6587\u672c\u4f53\u6548\u679c\u3011\n1\u4ef6\uff1a\u53d7\u5230\u602a\u7269\u9020\u6210\u7684\u4f24\u5bb3\u964d\u4f4e25%\u3002\n2\u4ef6\uff1a\u53d7\u5230\u602a\u7269\u653b\u51fb\u65f6\uff0c\u5bf9\u653b\u51fb\u8005\u9020\u6210\u8346\u68d8\u4f24\u5bb3\u3002\u57fa\u7840\u8346\u68d8\u503c\u4e3a\uff08\u81ea\u8eab10%\u6700\u5927\u751f\u547d\u503c+\u4e3b\u8981\u5c5e\u6027+\u5e78\u8fd0\uff09\u00d730%\u3002\n3\u4ef6\uff1a\u8346\u68d8\u4f24\u5bb3\u63d0\u9ad8100%\u3002\n4\u4ef6\uff1a\u6bcf\u6b21\u65bd\u653e\u6280\u80fd\u540e\uff0c\u4e0b\u4e00\u6b21\u5bf9\u654c\u4eba\u9020\u6210\u4f24\u5bb3\u65f6\uff0c\u8ffd\u52a0150%\u5f53\u524d\u8346\u68d8\u503c\u7684\u4f24\u5bb3\u3002\n5\u4ef6\uff1a\u53d7\u5230\u602a\u7269\u653b\u51fb\u65f6\uff0c\u81ea\u52a8\u91ca\u653eThorn Counter\u3002\n\n\u3010\u5957\u88c5\u989d\u5916\u6548\u679c\u3011\n2\u4ef6\u5957\u00b7\u5012\u523a\u6c72\u53d6\uff1a\u8346\u68d8\u4f24\u5bb3\u6210\u529f\u547d\u4e2d\u540e\u6062\u590d2%\u6700\u5927\u751f\u547d\u503c\uff0c\u6bcf1\u79d2\u6700\u591a1\u6b21\u3002\n4\u4ef6\u5957\u00b7\u8346\u68d8\u53cd\u566c\uff1a\u53d7\u5230\u602a\u7269\u4f24\u5bb3\u65f6\uff0c\u672c\u6b21\u4f24\u5bb3\u989d\u5916\u964d\u4f4e30%\uff0c\u5e76\u5bf9\u653b\u51fb\u8005\u9020\u6210200%\u5f53\u524d\u8346\u68d8\u503c\u7684\u8346\u68d8\u4f24\u5bb3\u30025\u79d2\u51b7\u5374\u3002\n5\u4ef6\u5957\u00b7\u4e07\u523a\u53cd\u51fb\uff1a\u8346\u68d8\u4f24\u5bb3\u989d\u5916\u63d0\u9ad8100%\uff0cThorn Counter\u51b7\u5374\u7f29\u77ed\u81f32\u79d2\u3002\u51fb\u6740\u7cbe\u82f1\u6216Boss\u65f6\uff0c\u8346\u68d8\u6548\u7387\u6c38\u4e45+10%\uff0c\u6700\u9ad8300%\u3002", "\u3010\u30eb\u30fc\u30f3\u672c\u4f53\u3011\n1\u500b\uff1a\u30e2\u30f3\u30b9\u30bf\u30fc\u304b\u3089\u306e\u30c0\u30e1\u30fc\u30b8-25%\u3002\n2\u500b\uff1a\u30e2\u30f3\u30b9\u30bf\u30fc\u306b\u653b\u6483\u3055\u308c\u308b\u3068\u653b\u6483\u8005\u306b\u68d8\u30c0\u30e1\u30fc\u30b8\u3002\n3\u500b\uff1a\u68d8\u30c0\u30e1\u30fc\u30b8+100%\u3002\n4\u500b\uff1a\u30b9\u30ad\u30eb\u5f8c\u306e\u6b21\u306e\u5bfe\u654c\u30d2\u30c3\u30c8\u306b\u73fe\u5728\u68d8\u5024150%\u5206\u3092\u8ffd\u52a0\u3002\n5\u500b\uff1a\u88ab\u5f3e\u6642\u306bThorn Counter\u3092\u81ea\u52d5\u767a\u52d5\u3002\n\n\u3010\u30bb\u30c3\u30c8\u8ffd\u52a0\u52b9\u679c\u3011\n2\u500b\u30fb\u68d8\u306e\u5438\u53ce\uff1a\u68d8\u30c0\u30e1\u30fc\u30b8\u547d\u4e2d\u6642\u3001\u6700\u5927HP2%\u56de\u5fa9\u30021\u79d2\u306b1\u56de\u307e\u3067\u3002\n4\u500b\u30fb\u68d8\u306e\u53cd\u566c\uff1a\u30e2\u30f3\u30b9\u30bf\u30fc\u30c0\u30e1\u30fc\u30b8\u3092\u3055\u3089\u306b30%\u8efd\u6e1b\u3057\u3001\u653b\u6483\u8005\u306b\u73fe\u5728\u68d8\u5024200%\u306e\u68d8\u30c0\u30e1\u30fc\u30b8\u30025\u79d2CD\u3002\n5\u500b\u30fb\u5343\u306e\u68d8\uff1a\u68d8\u30c0\u30e1\u30fc\u30b8+100%\u3001Thorn Counter CD2\u79d2\u3002\u30a8\u30ea\u30fc\u30c8/Boss\u6483\u7834\u3067\u68d8\u52b9\u7387+10%\uff08\u6700\u5927300%\uff09\u3002" } }
        ,{ "rune.luck.name", new[] { "Luck Rune", "\u5e78\u8fd0\u7b26\u6587", "\u5e78\u904b\u30eb\u30fc\u30f3" } }
        ,{ "rune.luck.flavor", new[] { "A fate rune that turns chance into combat rewards and long-term growth.", "\u7531\u547d\u8fd0\u6c14\u606f\u51dd\u7ed3\u800c\u6210\u7684\u7b26\u6587\uff0c\u80fd\u5c06\u6218\u6597\u4e2d\u7684\u5076\u7136\u6536\u76ca\u8f6c\u5316\u4e3a\u957f\u671f\u6210\u957f\u3002", "\u904b\u547d\u306e\u6c17\u914d\u304b\u3089\u751f\u307e\u308c\u305f\u30eb\u30fc\u30f3\u3002\u5076\u7136\u306e\u6069\u6075\u3092\u6210\u9577\u306b\u5909\u3048\u308b\u3002" } }
        ,{ "rune.luck.full_description", new[] { "縲審une Effects縲曾n1 Piece: Luck +5.\n2 Pieces: When a Growth Soul is generated, 30% chance to increase its point by 1, up to 5.\n3 Pieces: Kills have a 25% chance to drop 1 extra Growth Soul.\n4 Pieces: Picking up any Soul has a 20% chance to copy 1 Soul of the same type. The copy is fixed at 2 points.\n5 Pieces: Elite/Boss kills drop 5 extra Growth Souls.\n\n縲心et Bonuses縲曾n2 Pieces - Fate Roll: Each successful skill cast has a 20% chance to trigger a fate lottery from equipped non-Luck rune types. If no other rune type is equipped, restore 5% max HP and 5% max Mana.\n4 Pieces - Double Grace: Lottery chance becomes 35%. On success, draw two different results; if only one result exists, the second triggers at 50% value.\n5 Pieces - Fate Jackpot: On a successful lottery, 15% chance to trigger all current lottery results instead of normal draws. Elite/Boss kills grant permanent Luck Efficiency +10%, up to 300%. Luck Efficiency affects lottery and jackpot chance, clamped to 100%.", "\u3010\u7b26\u6587\u672c\u4f53\u6548\u679c\u3011\n1\u4ef6\uff1a\u5e78\u8fd0+5\u3002\n2\u4ef6\uff1a\u6210\u957f\u4e4b\u9b42\u751f\u6210\u65f6\uff0c\u670930%\u6982\u7387\u4f7f\u5176\u70b9\u6570+1\uff0c\u6700\u9ad85\u70b9\u3002\n3\u4ef6\uff1a\u51fb\u6740\u654c\u4eba\u65f6\uff0c\u670925%\u6982\u7387\u989d\u5916\u6389\u843d1\u4e2a\u6210\u957f\u4e4b\u9b42\u3002\n4\u4ef6\uff1a\u62fe\u53d6\u4efb\u610f\u7075\u9b42\u65f6\uff0c\u670920%\u6982\u7387\u989d\u5916\u590d\u52361\u4e2a\u540c\u7c7b\u7075\u9b42\uff0c\u590d\u5236\u7075\u9b42\u56fa\u5b9a\u4e3a2\u70b9\u3002\n5\u4ef6\uff1a\u51fb\u6740\u7cbe\u82f1\u654c\u4eba\u6216Boss\u65f6\uff0c\u989d\u5916\u6389\u843d5\u4e2a\u6210\u957f\u4e4b\u9b42\u3002\n\n\u3010\u5957\u88c5\u989d\u5916\u6548\u679c\u3011\n2\u4ef6\u5957\u00b7\u547d\u8fd0\u4e00\u63b7\uff1a\u6bcf\u6b21\u6210\u529f\u65bd\u653e\u6280\u80fd\u65f6\uff0c\u670920%\u6982\u7387\u8fdb\u884c\u547d\u8fd0\u62bd\u5956\uff0c\u4ece\u5f53\u524d\u88c5\u5907\u7684\u975e\u5e78\u8fd0\u7b26\u6587\u79cd\u7c7b\u4e2d\u62bd\u53d61\u79cd\u5e76\u89e6\u53d1\u5bf9\u5e94\u62df\u6001\u6548\u679c\u3002\u82e5\u6ca1\u6709\u5176\u4ed6\u7b26\u6587\uff0c\u5219\u6062\u590d5%\u6700\u5927\u751f\u547d\u503c\u548c5%\u6700\u5927\u9b54\u529b\u503c\u3002\n4\u4ef6\u5957\u00b7\u53cc\u91cd\u7737\u987e\uff1a\u547d\u8fd0\u62bd\u5956\u57fa\u7840\u6982\u7387\u63d0\u9ad8\u81f335%\u3002\u6210\u529f\u65f6\u62bd\u53d6\u4e24\u4e2a\u4e0d\u540c\u7ed3\u679c\uff1b\u82e5\u53ea\u67091\u79cd\u7ed3\u679c\uff0c\u7b2c2\u6b21\u6548\u679c\u964d\u4e3a50%\u3002\n5\u4ef6\u5957\u00b7\u547d\u8fd0\u5927\u5956\uff1a\u547d\u8fd0\u62bd\u5956\u6210\u529f\u65f6\uff0c\u670915%\u6982\u7387\u6539\u4e3a\u89e6\u53d1\u5f53\u524d\u62bd\u5956\u6c60\u4e2d\u5168\u90e8\u7ed3\u679c\u3002\u51fb\u6740\u7cbe\u82f1\u6216Boss\u65f6\uff0c\u5e78\u8fd0\u6548\u7387\u6c38\u4e45+10%\uff0c\u6700\u9ad8300%\u3002\u5e78\u8fd0\u6548\u7387\u5f71\u54cd\u62bd\u5956\u548c\u5927\u5956\u6982\u7387\uff0c\u6700\u7ec8\u6982\u7387\u4e0d\u8d85\u8fc7100%\u3002", "\u3010\u30eb\u30fc\u30f3\u672c\u4f53\u3011\n1\u500b\uff1a\u904b+5\u3002\n2\u500b\uff1a\u6210\u9577\u30bd\u30a6\u30eb\u751f\u6210\u6642\u300130%\u306e\u78ba\u7387\u3067\u5024+1\uff08\u6700\u59275\uff09\u3002\n3\u500b\uff1a\u6575\u3092\u5012\u3059\u306825%\u306e\u78ba\u7387\u3067\u6210\u9577\u30bd\u30a6\u30eb\u30921\u500b\u8ffd\u52a0\u30c9\u30ed\u30c3\u30d7\u3002\n4\u500b\uff1a\u30bd\u30a6\u30eb\u53d6\u5f97\u6642\u300120%\u306e\u78ba\u7387\u3067\u540c\u7a2e\u30bd\u30a6\u30eb\u30921\u500b\u30b3\u30d4\u30fc\u3002\u5024\u306f2\u56fa\u5b9a\u3002\n5\u500b\uff1a\u30a8\u30ea\u30fc\u30c8/Boss\u6483\u7834\u6642\u3001\u6210\u9577\u30bd\u30a6\u30eb\u30925\u500b\u8ffd\u52a0\u30c9\u30ed\u30c3\u30d7\u3002\n\n\u3010\u30bb\u30c3\u30c8\u8ffd\u52a0\u52b9\u679c\u3011\n2\u500b\u30fb\u904b\u547d\u306e\u4e00\u6295\uff1a\u30b9\u30ad\u30eb\u6210\u529f\u6642\u300120%\u306e\u78ba\u7387\u3067\u5e78\u904b\u4ee5\u5916\u306e\u88c5\u5099\u30eb\u30fc\u30f3\u304b\u30891\u7a2e\u985e\u3092\u62bd\u9078\u3057\u3001\u5bfe\u5fdc\u3059\u308b\u6069\u6075\u3092\u767a\u52d5\u3002\u4ed6\u306e\u30eb\u30fc\u30f3\u304c\u306a\u3051\u308c\u3070\u6700\u5927HP5%\u3068\u6700\u5927MP5%\u3092\u56de\u5fa9\u3002\n4\u500b\u30fb\u4e8c\u91cd\u306e\u52a0\u8b77\uff1a\u62bd\u9078\u78ba\u7387\u304c35%\u306b\u306a\u308a\u3001\u6210\u529f\u6642\u306b\u7570\u306a\u308b2\u7d50\u679c\u3092\u767a\u52d5\u30021\u7a2e\u985e\u3060\u3051\u306a\u3089\u540c\u3058\u7d50\u679c\u3092\u3082\u3046\u4e00\u5ea6\u767a\u52d5\u3057\u30012\u56de\u76ee\u306f50%\u52b9\u679c\u3002\n5\u500b\u30fb\u904b\u547d\u306e\u5927\u5f53\u305f\u308a\uff1a\u62bd\u9078\u6210\u529f\u6642\u300115%\u306e\u78ba\u7387\u3067\u62bd\u9078\u6c60\u306e\u5168\u7d50\u679c\u3092\u767a\u52d5\u3002\u30a8\u30ea\u30fc\u30c8/Boss\u6483\u7834\u3067\u5e78\u904b\u52b9\u7387+10%\uff08\u6700\u5927300%\uff09\u3002\u78ba\u7387\u306f\u6700\u5927100%\u3002" } }
        ,{ "Common", new[] { "Common", "\u666e\u901a", "\u30b3\u30e2\u30f3" } }
        ,{ "Selected Rune", new[] { "Selected Rune", "\u5df2\u9009\u7b26\u6587", "\u9078\u629e\u4e2d\u306e\u30eb\u30fc\u30f3" } }
        ,{ "Rune Name", new[] { "Rune Name", "\u7b26\u6587\u540d\u79f0", "\u30eb\u30fc\u30f3\u540d" } }
        ,{ "Type", new[] { "Type", "\u7c7b\u578b", "\u7a2e\u5225" } }
        ,{ "Effect", new[] { "Effect", "\u6548\u679c", "\u52b9\u679c" } }
        ,{ "Rune Effects", new[] { "Rune Effects", "\u7b26\u6587\u6548\u679c", "\u30eb\u30fc\u30f3\u52b9\u679c" } }
        ,{ "rune.empty_slot", new[] { "Empty Rune Slot", "\u7a7a\u7b26\u6587\u69fd", "\u7a7a\u306e\u30eb\u30fc\u30f3\u30b9\u30ed\u30c3\u30c8" } }
        ,{ "rune.select_prompt", new[] { "Select a rune", "\u8bf7\u9009\u62e9\u7b26\u6587", "\u30eb\u30fc\u30f3\u3092\u9078\u629e" } }
        ,{ "rune.equip_prompt", new[] { "Equip selected rune to skill slot", "\u5c06\u9009\u4e2d\u7684\u7b26\u6587\u9576\u5d4c\u5230\u6280\u80fd\u69fd", "\u9078\u629e\u3057\u305f\u30eb\u30fc\u30f3\u3092\u30b9\u30ad\u30eb\u30b9\u30ed\u30c3\u30c8\u306b\u88c5\u7740" } }
        ,{ "skill.1.q.title", new[] { "Q - Scissor Flurry", "Q - 快刀剪乱", "Q - 鋏乱舞" } }
        ,{ "skill.1.q.cooldown", new[] { "Cooldown: 3s", "\u51b7\u5374\u65f6\u95f4\uff1a3\u79d2", "\u30af\u30fc\u30eb\u30c0\u30a6\u30f3\uff1a3\u79d2" } }
        ,{ "skill.1.q.cost", new[] { "Cost: Medium Mana", "\u6d88\u8017\uff1a\u4e2d\u7b49\u9b54\u529b", "\u6d88\u8cbb\uff1a\u4e2d\u7a0b\u5ea6\u306e\u30de\u30ca" } }
        ,{ "skill.1.q.range", new[] { "Range: Melee", "\u8303\u56f4\uff1a\u8fd1\u6218", "\u7bc4\u56f2\uff1a\u8fd1\u63a5" } }
        ,{ "skill.1.q.damage", new[] { "Damage: Quick physical burst", "\u4f24\u5bb3\uff1a\u5feb\u901f\u7269\u7406\u7206\u53d1", "\u30c0\u30e1\u30fc\u30b8\uff1a\u7d20\u65e9\u3044\u7269\u7406\u30d0\u30fc\u30b9\u30c8" } }
        ,{ "skill.1.q.description", new[] { "Spiritweave Doll rapidly swings her scissors, dealing mixed physical and magic damage to nearby enemies in front.\nThis skill has a built-in 30% critical rate and a 5% chance to deal 5x damage.\nAfter dealing damage, Spiritweave Doll restores HP equal to 50% of the actual damage dealt.", "灵罗娃娃快速挥动剪刀，对前方近距离敌人造成物理与魔法的混合伤害。\n该技能自带30%暴击率，并有5%概率造成5倍伤害。\n技能造成伤害后，灵罗娃娃会回复相当于本次实际伤害50%的生命值。", "霊羅人形が素早く鋏を振るい、前方近距離の敵に物理と魔法の混合ダメージを与える。\nこのスキルは30%の固有クリティカル率を持ち、5%の確率で5倍ダメージを与える。\nダメージを与えた後、霊羅人形は今回の実ダメージの50%に相当するHPを回復する。" } }
        ,{ "skill.1.w.title", new[] { "W - Starveil Curtain", "W - 星辰帷幕", "W - 星辰の帷幕" } }
        ,{ "skill.1.w.cooldown", new[] { "Cooldown: 5s", "\u51b7\u5374\u65f6\u95f4\uff1a5\u79d2", "\u30af\u30fc\u30eb\u30c0\u30a6\u30f3\uff1a5\u79d2" } }
        ,{ "skill.1.w.cost", new[] { "Cost: Medium Mana", "\u6d88\u8017\uff1a\u4e2d\u7b49\u9b54\u529b", "\u6d88\u8cbb\uff1a\u4e2d\u7a0b\u5ea6\u306e\u30de\u30ca" } }
        ,{ "skill.1.w.range", new[] { "Range: Self / Defense", "\u8303\u56f4\uff1a\u81ea\u8eab / \u9632\u5fa1", "\u7bc4\u56f2\uff1a\u81ea\u5206 / \u9632\u5fa1" } }
        ,{ "skill.1.w.damage", new[] { "Damage: Defensive support", "\u4f24\u5bb3\uff1a\u9632\u5fa1\u8f85\u52a9", "\u30c0\u30e1\u30fc\u30b8\uff1a\u9632\u5fa1\u652f\u63f4" } }
        ,{ "skill.1.w.description", new[] { "Spiritweave Doll unfolds Starveil Curtain, causing the barrier to surround her and follow her movement.\nWhile Starveil Curtain lasts, damage taken by Spiritweave Doll is reduced by 50%.\nEnemies inside the area have reduced movement speed, attack frequency, and damage dealt.", "灵罗娃娃展开星辰帷幕，使结界环绕自身并跟随移动。\n帷幕持续期间，灵罗娃娃受到的伤害降低50%。\n范围内敌人的移动速度、攻击频率和造成的伤害都会降低。", "霊羅人形が星辰の帷幕を展開し、結界が自身を囲んで移動に追従する。\n帷幕の持続中、霊羅人形が受けるダメージは50%低下する。\n範囲内の敵は移動速度、攻撃頻度、与えるダメージが低下する。" } }
        ,{ "skill.1.e.title", new[] { "E - Spirit Sprint", "E - 灵体疾行", "E - 霊体疾走" } }
        ,{ "skill.1.e.cooldown", new[] { "Cooldown: 8s", "\u51b7\u5374\u65f6\u95f4\uff1a8\u79d2", "\u30af\u30fc\u30eb\u30c0\u30a6\u30f3\uff1a8\u79d2" } }
        ,{ "skill.1.e.cost", new[] { "Cost: Medium Mana", "\u6d88\u8017\uff1a\u4e2d\u7b49\u9b54\u529b", "\u6d88\u8cbb\uff1a\u4e2d\u7a0b\u5ea6\u306e\u30de\u30ca" } }
        ,{ "skill.1.e.range", new[] { "Range: Movement", "\u8303\u56f4\uff1a\u4f4d\u79fb", "\u7bc4\u56f2\uff1a\u79fb\u52d5" } }
        ,{ "skill.1.e.damage", new[] { "Damage: Low contact damage", "\u4f24\u5bb3\uff1a\u4f4e\u63a5\u89e6\u4f24\u5bb3", "\u30c0\u30e1\u30fc\u30b8\uff1a\u4f4e\u3044\u63a5\u89e6\u30c0\u30e1\u30fc\u30b8" } }
        ,{ "skill.1.e.description", new[] { "Spiritweave Doll enters a spirit state, increasing movement speed and allowing her to pass through obstacles and enemies.\nWhile in spirit state, she continuously restores HP and is immune to physical attack damage from monsters.\nWhen the state ends, she returns to normal.", "灵罗娃娃进入灵体状态，提升移动速度，并可穿过障碍物和敌人。\n灵体状态期间会持续回复生命值，并免疫怪物造成的物理攻击伤害。\n状态结束后恢复正常。", "霊羅人形が霊体状態に入り、移動速度が上昇し、障害物や敵を通り抜けられる。\n霊体状態中は継続的にHPを回復し、モンスターの物理攻撃ダメージを受けない。\n状態終了後は通常に戻る。" } }
        ,{ "skill.1.r.title", new[] { "R - Needle Shot", "R - 飞针射击", "R - 飛針射撃" } }
        ,{ "skill.1.r.cooldown", new[] { "Cooldown: 25s", "冷却时间：25秒", "クールダウン：25秒" } }
        ,{ "skill.1.r.cost", new[] { "Cost: 60 Mana", "消耗：60魔力", "消費：60マナ" } }
        ,{ "skill.1.r.range", new[] { "Range: Forward thrust / target-point needles", "范围：前方近战 / 目标周围飞针", "範囲：前方近接 / 目標周辺の飛針" } }
        ,{ "skill.1.r.damage", new[] { "Damage: Mixed thrust and needle damage", "伤害：前刺与飞针混合伤害", "ダメージ：突きと飛針の混合ダメージ" } }
        ,{ "skill.1.r.description", new[] { "Spiritweave Doll first performs a short thrust, then summons 4-12 needles to pierce nearby enemies.\nNeedles fly in straight lines and can pierce multiple enemies; the same needle can hit the same enemy at most once.\nNight Child increases this skill's damage, and Mana Rune enhancement strengthens both the thrust and the needles.", "灵罗娃娃先发动一次近距离前刺，随后连续召出4～12枚飞针穿刺周围敌人。\n飞针沿直线飞行，可以穿透多个敌人；同一枚飞针对同一敌人最多命中一次。\n夜之子会提高本技能伤害，魔力符文强化会同时增强前刺与飞针。", "霊羅人形が近距離の突きを放ち、その後4～12本の飛針を連続で召喚して周囲の敵を貫く。\n飛針は直線で飛び、複数の敵を貫通できる。同じ飛針が同じ敵に命中するのは最大1回まで。\n夜の子状態ではこのスキルのダメージが上昇し、マナルーン強化は突きと飛針の両方を強化する。" } }
        ,{ "skill.2.q.title", new[] { "Q - Starblade Fall", "Q - 星刃坠落", "Q - 星刃落下" } }
        ,{ "skill.2.q.cooldown", new[] { "Cooldown: 0.8s", "\u51b7\u5374\u65f6\u95f4\uff1a0.8\u79d2", "\u30af\u30fc\u30eb\u30c0\u30a6\u30f3\uff1a0.8\u79d2" } }
        ,{ "skill.2.q.cost", new[] { "Cost: 10 Mana", "\u6d88\u8017\uff1a10\u9b54\u529b", "\u6d88\u8cbb\uff1a10\u30de\u30ca" } }
        ,{ "skill.2.q.range", new[] { "Range: Targeted area", "\u8303\u56f4\uff1a\u6307\u5b9a\u843d\u70b9\u533a\u57df", "\u7bc4\u56f2\uff1a\u6307\u5b9a\u5730\u70b9\u30a8\u30ea\u30a2" } }
        ,{ "skill.2.q.damage", new[] { "Damage: Multi-hit mixed sword rain", "\u4f24\u5bb3\uff1a\u591a\u6bb5\u6df7\u5408\u5251\u96e8", "\u30c0\u30e1\u30fc\u30b8\uff1a\u591a\u6bb5\u30df\u30c3\u30af\u30b9\u5263\u96e8" } }
        ,{ "skill.2.q.description", new[] { "Chosen Child summons starblades to fall randomly from the sky, dealing mixed physical and magic damage to enemies.\nInitially summons 1-3 random starblades; as skill level rises, this can reach up to 1-7.\nEach starblade checks hits independently, so they may not all hit the same target.", "神眷之子召唤星刃从空中随机坠落，对敌人造成物理与魔法的混合伤害。\n初始会随机召唤1～3把星刃，随技能等级提升，最多可召唤1～7把。\n每把星刃独立判定命中，不一定全部命中同一目标。", "神眷の子が星刃を空からランダムに落下させ、敵に物理と魔法の混合ダメージを与える。\n初期は1～3本をランダムに召喚し、スキルレベル上昇で最大1～7本になる。\n各星刃は独立して命中判定を行うため、すべてが同じ対象に命中するとは限らない。" } }
        ,{ "skill.2.w.title", new[] { "W - Starblade Shield", "W - 星刃护盾", "W - 星刃シールド" } }
        ,{ "skill.2.w.cooldown", new[] { "Cooldown: 6s", "\u51b7\u5374\u65f6\u95f4\uff1a6\u79d2", "\u30af\u30fc\u30eb\u30c0\u30a6\u30f3\uff1a6\u79d2" } }
        ,{ "skill.2.w.cost", new[] { "Cost: 40 Mana", "\u6d88\u8017\uff1a40\u9b54\u529b", "\u6d88\u8cbb\uff1a40\u30de\u30ca" } }
        ,{ "skill.2.w.range", new[] { "Range: Self / Orbit", "\u8303\u56f4\uff1a\u81ea\u8eab / \u73af\u7ed5", "\u7bc4\u56f2\uff1a\u81ea\u5206 / \u5468\u56de" } }
        ,{ "skill.2.w.damage", new[] { "Damage: Low damage, defensive", "\u4f24\u5bb3\uff1a\u4f4e\u4f24\u5bb3\uff0c\u504f\u9632\u5fa1", "\u30c0\u30e1\u30fc\u30b8\uff1a\u4f4e\u30c0\u30e1\u30fc\u30b8\u30fb\u9632\u5fa1\u5bc4\u308a" } }
        ,{ "skill.2.w.description", new[] { "Chosen Child unfolds a sacred shield and summons starblades to rotate around them.\nThe shield value equals 100% of their max HP.\nRotating starblades block approaching enemies and deal shield-based mixed damage to enemies they touch.", "神眷之子展开神圣护盾，并召唤星刃环绕自身旋转。\n护盾值相当于自身100%最大生命值。\n旋转星刃会阻挡靠近的敌人，并对接触的敌人造成基于护盾值的混合伤害。", "神眷の子が神聖なシールドを展開し、星刃を召喚して自身の周囲を回転させる。\nシールド値は自身の最大HP100%分。\n回転する星刃は近づく敵を阻み、接触した敵にシールド値に基づく混合ダメージを与える。" } }
        ,{ "skill.2.e.title", new[] { "E - Startrail Shift", "E - 星痕瞬移", "E - 星痕シフト" } }
        ,{ "skill.2.e.cooldown", new[] { "Cooldown: 8s", "\u51b7\u5374\u65f6\u95f4\uff1a8\u79d2", "\u30af\u30fc\u30eb\u30c0\u30a6\u30f3\uff1a8\u79d2" } }
        ,{ "skill.2.e.cost", new[] { "Cost: 20 Mana", "\u6d88\u8017\uff1a20\u9b54\u529b", "\u6d88\u8cbb\uff1a20\u30de\u30ca" } }
        ,{ "skill.2.e.range", new[] { "Range: Dash", "\u8303\u56f4\uff1a\u51b2\u523a", "\u7bc4\u56f2\uff1a\u30c0\u30c3\u30b7\u30e5" } }
        ,{ "skill.2.e.damage", new[] { "Damage: Utility movement", "\u4f24\u5bb3\uff1a\u529f\u80fd\u6027\u4f4d\u79fb", "\u30c0\u30e1\u30fc\u30b8\uff1a\u6a5f\u80fd\u7684\u306a\u79fb\u52d5" } }
        ,{ "skill.2.e.description", new[] { "Chosen Child turns into starlight and dashes in the chosen direction, dealing mixed physical and magic damage to enemies along the path.\nDuring the same Startrail Shift, each enemy can take path damage at most once.", "神眷之子化作星光向指定方向高速位移，并对路径上的敌人造成物理与魔法的混合伤害。\n同一次星痕瞬移中，每个敌人最多受到一次路径伤害。", "神眷の子が星光となって指定方向へ高速移動し、経路上の敵に物理と魔法の混合ダメージを与える。\n同じ星痕シフト中、各敵が経路ダメージを受けるのは最大1回まで。" } }
        ,{ "skill.2.r.title", new[] { "R - Divine Ring Star Rain", "R - 神环星雨", "R - 神環星雨" } }
        ,{ "skill.2.r.cooldown", new[] { "Cooldown: 15s", "\u51b7\u5374\u65f6\u95f4\uff1a15\u79d2", "\u30af\u30fc\u30eb\u30c0\u30a6\u30f3\uff1a15\u79d2" } }
        ,{ "skill.2.r.cost", new[] { "Cost: 60 Mana", "\u6d88\u8017\uff1a60\u9b54\u529b", "\u6d88\u8cbb\uff1a60\u30de\u30ca" } }
        ,{ "skill.2.r.range", new[] { "Range: Large vortex area", "\u8303\u56f4\uff1a\u5927\u8303\u56f4\u6f29\u6da1\u533a\u57df", "\u7bc4\u56f2\uff1a\u5927\u7bc4\u56f2\u306e\u6e26\u30a8\u30ea\u30a2" } }
        ,{ "skill.2.r.damage", new[] { "Damage: Continuous multi-hit special damage", "\u4f24\u5bb3\uff1a\u6301\u7eed\u591a\u6bb5\u7279\u6b8a\u4f24\u5bb3", "\u30c0\u30e1\u30fc\u30b8\uff1a\u7d99\u7d9a\u591a\u6bb5\u7279\u6b8a\u30c0\u30e1\u30fc\u30b8" } }
        ,{ "skill.2.r.description", new[] { "Chosen Child unfolds a rotating starblade formation, dealing mixed physical and magic damage to enemies it touches.\nEach starblade checks hits independently, and the same enemy can be hit by multiple starblades in sequence.\nThe more starblades are currently active, the longer the skill lasts and the faster they rotate.", "神眷之子展开环绕自身的星刃阵，持续旋转并对接触的敌人造成物理与魔法的混合伤害。\n每把星刃独立判定命中，同一敌人可以被多把星刃连续命中。\n当前星刃数量越多，技能持续时间越长，旋转速度越快。", "神眷の子が回転する星刃陣を展開し、接触した敵に物理と魔法の混合ダメージを与える。\n各星刃は独立して命中判定を行い、同じ敵が複数の星刃から連続して命中を受けることがある。\n現在の星刃数が多いほど、スキルの持続時間が長くなり、回転速度も速くなる。" } }
        ,{ "Attributes", new[] { "Attributes", "\u5c5e\u6027", "\u80fd\u529b" } }
        ,{ "Character Attributes", new[] { "Character Attributes", "\u89d2\u8272\u5c5e\u6027", "\u30ad\u30e3\u30e9\u30af\u30bf\u30fc\u80fd\u529b" } }
        ,{ "character.player01.name", new[] { "Spiritweave Doll", "\u7075\u7f57\u5a03\u5a03", "\u970a\u7f85\u4eba\u5f62" } }
        ,{ "character.player02.name", new[] { "Chosen Child", "\u795e\u7737\u4e4b\u5b50", "\u795e\u7737\u306e\u5b50" } }
        ,{ "character.attributes.title", new[] { "{0} Attributes", "{0}\u5c5e\u6027", "{0}\u306e\u80fd\u529b" } }
        ,{ "Character Preview", new[] { "Character Preview", "\u89d2\u8272\u9884\u89c8", "\u30ad\u30e3\u30e9\u30af\u30bf\u30fc\u30d7\u30ec\u30d3\u30e5\u30fc" } }
        ,{ "LUCK", new[] { "LUCK", "\u5e78\u8fd0", "\u904b" } }
        ,{ "Crit Rate", new[] { "Crit Rate", "\u66b4\u51fb\u7387", "\u30af\u30ea\u30c6\u30a3\u30ab\u30eb\u7387" } }
        ,{ "Extra Soul Drop", new[] { "Extra Soul Drop", "\u989d\u5916\u7075\u9b42\u6389\u843d", "\u8ffd\u52a0\u30bd\u30a6\u30eb\u30c9\u30ed\u30c3\u30d7" } }
        ,{ "Extra Rune Drop", new[] { "Extra Rune Drop", "\u989d\u5916\u7b26\u6587\u6389\u843d", "\u8ffd\u52a0\u30eb\u30fc\u30f3\u30c9\u30ed\u30c3\u30d7" } }
    };

    private readonly Dictionary<TextMeshProUGUI, TMP_FontAsset> originalFonts = new Dictionary<TextMeshProUGUI, TMP_FontAsset>();

    public GameLanguage CurrentLanguage { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        EnsureInstance();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStartupContext()
    {
        LaunchedFromMainMenu = false;
    }

    public static void MarkFormalGameStart()
    {
        LaunchedFromMainMenu = true;
    }

    public static GameLocalization EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameLocalization existing = FindObjectOfType<GameLocalization>();
        if (existing != null)
        {
            return existing;
        }

        GameObject localizationObject = new GameObject(RuntimeObjectName);
        return localizationObject.AddComponent<GameLocalization>();
    }

    public void SetCjkFont(TMP_FontAsset font)
    {
        if (font != null)
            cjkFont = font;

        ConfigureFallbackFonts();
        PreloadTranslationCharacters();
    }

    public void SetJapaneseFont(TMP_FontAsset font)
    {
        if (font != null)
            japaneseFont = font;

        ConfigureFallbackFonts();
        PreloadTranslationCharacters();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CurrentLanguage = ResolveInitialLanguage();
        ConfigureFallbackFonts();
        PreloadTranslationCharacters();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        ApplyToAllText();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    public void CycleLanguage()
    {
        SetLanguage((GameLanguage)(((int)CurrentLanguage + 1) % 3));
    }

    public void SetLanguage(GameLanguage language)
    {
        if (CurrentLanguage == language)
        {
            ApplyToAllText();
            return;
        }

        CurrentLanguage = language;
        PlayerPrefs.SetInt(PreferenceKey, (int)CurrentLanguage);
        PlayerPrefs.Save();
        ApplyToAllText();
        LanguageChanged?.Invoke(CurrentLanguage);
    }

    public string Translate(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;

        if (TryTranslate(key, out string translated))
        {
            return translated;
        }

        return key;
    }

    public string TranslateOrFallback(string key, string fallback)
    {
        return TryTranslate(key, out string translated) ? translated : fallback;
    }

    public string TranslateOrFallback(string key, string fallback, GameLanguage language)
    {
        return TryTranslate(key, language, out string translated) ? translated : fallback;
    }

    public string FormatOrFallback(string key, string fallbackFormat, params object[] args)
    {
        string format = TranslateOrFallback(key, fallbackFormat);
        return args == null || args.Length == 0 ? format : string.Format(format, args);
    }

    public bool TryTranslate(string key, out string translated)
    {
        return TryTranslate(key, CurrentLanguage, out translated);
    }

    private bool TryTranslate(string key, GameLanguage language, out string translated)
    {
        translated = key;
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        foreach (KeyValuePair<string, string[]> entry in translations)
        {
            if (entry.Key == key || Array.IndexOf(entry.Value, key) >= 0)
            {
                int languageIndex = Mathf.Clamp((int)language, 0, 2);
                translated = SanitizeTranslationText(entry.Value[languageIndex]);
                return true;
            }
        }

        return false;
    }

    private static string SanitizeTranslationText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return text
            .Replace("縲審une Effects縲曾n", "Rune Effects:\n")
            .Replace("縲心et Bonuses縲曾n", "Set Bonuses:\n");
    }

    private GameLanguage ResolveInitialLanguage()
    {
#if UNITY_EDITOR
        if (!LaunchedFromMainMenu && IsEditorDirectPlayBattleScene())
        {
            return DefaultLanguage;
        }
#endif

        return (GameLanguage)Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, (int)DefaultLanguage), 0, 2);
    }

#if UNITY_EDITOR
    private static bool IsEditorDirectPlayBattleScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        return activeScene.IsValid() && activeScene.name == DirectEditorBattleSceneName;
    }
#endif

    public void ApplyToText(TextMeshProUGUI text, string key = null)
    {
        if (text == null)
            return;

        string source = string.IsNullOrEmpty(key) ? text.text : key;
        string translated = Translate(source);
        if (translated == source)
            return;

        text.text = translated;
        ApplyFontForLanguage(text);
    }

    public void ApplyFontForLanguage(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        if (!originalFonts.ContainsKey(text))
            originalFonts.Add(text, text.font);

        if (CurrentLanguage == GameLanguage.English)
        {
            text.font = originalFonts[text];
        }
        else
        {
            TMP_FontAsset primaryFont = CurrentLanguage == GameLanguage.Japanese ? japaneseFont : cjkFont;
            if (primaryFont != null)
                text.font = primaryFont;
        }
    }

    private void ConfigureFallbackFonts()
    {
        AddFallback(cjkFont, japaneseFont);
        AddFallback(japaneseFont, cjkFont);
    }

    private void PreloadTranslationCharacters()
    {
        if (translations == null || translations.Count == 0)
            return;

        StringBuilder characters = new StringBuilder();
        foreach (KeyValuePair<string, string[]> entry in translations)
        {
            foreach (string value in entry.Value)
                characters.Append(value);
        }

        string characterSet = characters.ToString();
        string missingCharacters;
        if (cjkFont != null)
            cjkFont.TryAddCharacters(characterSet, out missingCharacters);

        if (japaneseFont != null)
            japaneseFont.TryAddCharacters(characterSet, out missingCharacters);
    }

    private static void AddFallback(TMP_FontAsset primary, TMP_FontAsset fallback)
    {
        if (primary == null || fallback == null || primary == fallback)
            return;

        if (primary.fallbackFontAssetTable == null)
            primary.fallbackFontAssetTable = new List<TMP_FontAsset>();

        if (!primary.fallbackFontAssetTable.Contains(fallback))
            primary.fallbackFontAssetTable.Add(fallback);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToAllText();
        StartCoroutine(ApplyAfterSceneInitialization());
    }

    private IEnumerator ApplyAfterSceneInitialization()
    {
        yield return null;
        ApplyToAllText();
    }

    private void ApplyToAllText()
    {
        foreach (TextMeshProUGUI text in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
        {
            if (text != null && text.gameObject.scene.IsValid())
                ApplyToText(text);
        }
    }
}
