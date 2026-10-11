/* =====================================================================
   表格样式：跟卡片一致的扁平观感 + 绿色打勾的复选框单元格
   ===================================================================== */
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

static class GridStyle
{
    public static void Apply(DataGridView g)
    {
        /* DataGridView 的 DoubleBuffered 是 protected，只能反射打开。
           不开的话它每次只重画变化的那几格，底下的旧像素留着不走。 */
        try
        {
            typeof(DataGridView).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(g, true, null);
        }
        catch { }

        g.BackgroundColor = Theme.Card;
        g.BorderStyle = BorderStyle.None;
        g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        g.ColumnHeadersHeight = 34;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        g.RowTemplate.Height = 30;
        g.AllowUserToResizeRows = false;
        g.SelectionMode = DataGridViewSelectionMode.CellSelect;
        g.GridColor = Theme.Border;
        g.Font = Theme.Base;

        g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
        g.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Muted;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(249, 250, 251);
        g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.Muted;
        g.ColumnHeadersDefaultCellStyle.Font = Theme.Base;
        g.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 0, 0);
        g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

        g.DefaultCellStyle.BackColor = Theme.Card;
        g.DefaultCellStyle.ForeColor = Theme.Text;
        /* DataGridView 不支持带 alpha 的颜色，给了半透明会直接渲染成黑色 */
        g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(228, 238, 252);
        g.DefaultCellStyle.SelectionForeColor = Theme.Text;
        g.DefaultCellStyle.Font = Theme.Base;
        g.DefaultCellStyle.Padding = new Padding(6, 0, 0, 0);
        g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 253, 254);

        /* 复选框自己画：方框 + 勾上变绿底白勾 */
        g.CellPainting += PaintCell;
    }

    static void PaintCell(object sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        DataGridView g = sender as DataGridView;
        if (g == null) return;
        if (!(g.Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn)) return;

        e.PaintBackground(e.CellBounds, true);

        bool on = false;
        object v = e.Value;
        if (v is bool) on = (bool)v;
        else if (v != null) { try { on = System.Convert.ToBoolean(v); } catch { on = false; } }

        bool hover = g.CurrentCell != null
                  && g.CurrentCell.RowIndex == e.RowIndex
                  && g.CurrentCell.ColumnIndex == e.ColumnIndex;

        int box = 17;
        Rectangle r = new Rectangle(
            e.CellBounds.X + (e.CellBounds.Width - box) / 2,
            e.CellBounds.Y + (e.CellBounds.Height - box) / 2,
            box, box);

        Theme.DrawCheck(e.Graphics, r, on, hover);
        e.Handled = true;
    }
}
