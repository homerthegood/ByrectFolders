using System.Windows.Automation;

namespace DirectFolders;

/// <summary>
/// 탐색기·바탕화면에서 휠 클릭 위치가 항목(파일·폴더·트리·탭 등) 위인지 UI Automation으로 판별한다.
/// 항목 위면 원래 동작(새 탭으로 열기, 탭 닫기 등)을 살리고, 빈 곳이면 즐겨찾기 메뉴를 띄운다.
/// </summary>
internal static class ExplorerHitTest
{
    private static readonly HashSet<ControlType> ItemTypes = new()
    {
        ControlType.ListItem, ControlType.TreeItem, ControlType.TabItem, ControlType.DataItem,
        ControlType.Hyperlink, ControlType.Button, ControlType.SplitButton, ControlType.MenuItem,
        ControlType.Edit, ControlType.ComboBox,
    };

    private static readonly HashSet<ControlType> ContainerTypes = new()
    {
        ControlType.List, ControlType.Tree, ControlType.Tab, ControlType.Window, ControlType.DataGrid,
    };

    public static bool IsOverItem(Native.POINT pt)
    {
        try
        {
            var el = AutomationElement.FromPoint(new System.Windows.Point(pt.X, pt.Y));
            var walker = TreeWalker.ControlViewWalker;
            for (int depth = 0; el != null && depth < 8; depth++)
            {
                var type = el.Current.ControlType;
                if (ItemTypes.Contains(type))
                {
                    Log.Write($"hit test: item ({type.ProgrammaticName}) → 원래 동작 통과");
                    return true;
                }
                if (ContainerTypes.Contains(type))
                {
                    Log.Write($"hit test: empty ({type.ProgrammaticName}) → 메뉴");
                    return false;
                }
                el = walker.GetParent(el);
            }
        }
        catch (Exception ex)
        {
            Log.Write("hit test failed: " + ex.Message);
        }
        return false;
    }
}
