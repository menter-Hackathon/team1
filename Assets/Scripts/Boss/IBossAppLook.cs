/// <summary>
/// ボスの見た目を、そのアプリのもの（アイコンのドット絵・色）にする窓口（2026-11-19：AI チャットのボス）。
/// StageDirector が、ボスを作った直後（出てくる前）に、ボスとその子の部品すべてに渡す。
/// 代用ボス（対応表に無いアプリ）でなければ visual は null（そのときは各部品の初期の見た目のまま）
/// </summary>
public interface IBossAppLook
{
    void ApplyAppLook(string appName, AppVisual visual, AppTheme theme);
}
