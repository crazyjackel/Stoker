using System;
using System.Collections.Generic;
using System.Text;

namespace Stoker.Scripts
{
    /// <summary>
    /// DerivedSelectionButtons are Initializable SelectionButtons as Unity doesn't like generic components
    /// </summary>

    public class DerivedRelicStateSelectionButton : SelectionButton<RelicState>
    {
    }
    public class DerivedCardStateSelectionButton : SelectionButton<CardState>
    {
    }
    public class DerivedCardDataSelectionButton : SelectionButton<CardData>
    {
    }
    public class DerivedRelicDataSelectionButton : SelectionButton<RelicData>
    {
    }
}
