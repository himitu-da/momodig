/// <summary>
/// セーブ・ロード機能を提供するオブジェクトが実装するインターフェース
/// </summary>
public interface ISaveable
{
    /// <summary>
    /// このセーブデータが関連付けられるファイル名（拡張子なし）
    /// 例: "world", "player"
    /// </summary>
    string SaveFileName { get; }

    /// <summary>
    /// オブジェクトの状態をシリアライズ可能なデータに変換して返す
    /// </summary>
    /// <returns>シリアライズする状態データ</returns>
    object CaptureState();

    /// <summary>
    /// 指定されたデータからオブジェクトの状態を復元する
    /// </summary>
    /// <param name="state">デシリアライズされた状態データ</param>
    void RestoreState(object state);
}
