using System;
using System.Collections.Generic;

namespace Toggl_Exist.Toggl
{
    public class TimeEntry
    {
        public string project;
        public string description;
        public IReadOnlyList<string> tags;
        public IReadOnlyList<string> matchingTags;
        public DateTimeOffset start;
        public DateTimeOffset end;
    }
}
