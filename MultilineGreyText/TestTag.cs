using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace RefactAI
{
    //SpaceNegotiatingAdornmentTag are used to tell the editor to create empty space
    //they work like a more complicated version of <br> from html 
    internal class TestTag : SpaceNegotiatingAdornmentTag
    {
        public TestTag(double width, double topSpace, double baseline, double textHeight, double bottomSpace, PositionAffinity affinity, object identityTag, object providerTag) 
            : base(width, topSpace, baseline, textHeight, bottomSpace, affinity, identityTag, providerTag)
        {
        }
    }
}
