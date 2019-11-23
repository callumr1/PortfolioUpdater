using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortfolioUpdater.Models
{
    class MyProject
    {
        public int ProjectId { get; set; }

        public string ProjectName { get; set; }

        public string ProjectContent { get; set; }

        public List<string> ProjectLanguages { get; set; }

        public string LogoURL { get; set; }

        public string LogoAltText { get; set; }

        public string GithubURL { get; set; }

        public string ProjectURL { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
