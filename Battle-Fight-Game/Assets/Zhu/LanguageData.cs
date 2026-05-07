using UnityEngine;
// 这行是关键：让Unity右键能创建这个配置文件
[CreateAssetMenu(fileName = "LanguageData", menuName = "Game/LanguageData")]
public class LanguageData : ScriptableObject
{
    // 这里的字段对应游戏里所有需要翻译的文本，你可以按自己的UI加/减
    [Header("主菜单按钮文本")]
    public string startGame; // 开始游戏
    public string loadGame;  // 读取游戏
    public string saveGame;  // 保存游戏
    public string settings;  // 设置
    public string quitGame;  // 退出游戏

    [Header("设置面板文本")]
    public string music;      // 音乐
    public string sfx;        // 音效
    public string fullscreen; // 全屏
    public string language;   // 语言
    public string close;      // 关闭

    [Header("存档文本")]
    public string noSaveData; // 无存档数据
    public string lastSave;   // 上次存档时间：
}