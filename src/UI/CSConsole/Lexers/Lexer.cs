﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityExplorer.UI.CSConsole.Lexers
{
    public abstract class Lexer
    {
        public virtual IEnumerable<char> Delimiters => Enumerable.Empty<char>();

        protected abstract Color HighlightColor { get; }

        public string ColorTag => colorTag ?? (colorTag = "<color=#" + HighlightColor.ToHex() + ">");
        private string colorTag;

        public abstract bool TryMatchCurrent(LexerBuilder lexer);
    }
}
