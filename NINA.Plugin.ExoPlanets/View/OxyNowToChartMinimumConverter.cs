using OxyPlot.Axes;
using System;
using System.Globalization;
using System.Windows.Data;

namespace NINA.Plugin.ExoPlanets.View {

    /// <summary>
    /// Multi-value converter that computes the altitude chart's X-axis minimum.
    /// Bindings (in order):
    ///   [0] NighttimeData.Ticker.OxyNow  — used only as a live timer trigger
    ///   [1] NighttimeData.ReferenceDate  — the noon that starts the observing night
    ///
    /// Returns the later of (ReferenceDate, DateTime.Now), converted to an OxyPlot double.
    /// This keeps the chart anchored to the noon before the transit night, but starts at
    /// "now" when the user is browsing a future night that hasn't started yet.
    /// </summary>
    public class OxyNowToChartMinimumConverter : IMultiValueConverter {

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            DateTime referenceDate = DateTime.Now.Date; // fallback
            if (values.Length > 1 && values[1] is DateTime rd) {
                referenceDate = rd;
            }

            var now = DateTime.Now;
            var axisStart = now > referenceDate ? referenceDate : now;
            return DateTimeAxis.ToDouble(axisStart);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
