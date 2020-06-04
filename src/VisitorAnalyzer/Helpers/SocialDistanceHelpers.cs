using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Point = Windows.Foundation.Point;

namespace VisitorAnalyzer.Helpers
{
    public class SocialDistanceHelpers
    {
        public const double aPixelInCm = 438;
        public const double LimitDistance = 150;
        public static (bool Result, List<Windows.UI.Xaml.Shapes.Line> Lines) Detect(Rect[] Persons)
        {
            var res = false;
            var Lines = new List<Windows.UI.Xaml.Shapes.Line>();
            foreach(var person1 in Persons)
            {
                var p1 = new Point( person1.X+(person1.Width/2), person1.Y+(person1.Height/2));
                foreach (var person2 in Persons) {
                    if (person1 == person2) continue;
                    var p2 = new Point(person2.X + (person2.Width / 2), person2.Y + (person2.Height / 2));
                    var dist = DistanceHelpers.Euclidean(p1, p2)* aPixelInCm;
                    if(dist < LimitDistance)
                    {
                        res = true;
                        Lines.Add(new Windows.UI.Xaml.Shapes.Line() { X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y });
                    }

                }
            }
            return (res, Lines);
        }
    }
}
