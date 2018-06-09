using EnsureThat;
using JetBrains.Annotations;

namespace AlleyCat.UI
{
    public interface IHideableUI
    {
        bool Visible { get; set; }

        void Show();

        void Hide();
    }

    public static class HideableUIExtensions
    {
        public static void Toggle([NotNull] this IHideableUI ui)
        {
            Ensure.Any.IsNotNull(ui, nameof(ui));

            if (ui.Visible)
            {
                ui.Hide();
            }
            else
            {
                ui.Show();
            }
        }
    }
}