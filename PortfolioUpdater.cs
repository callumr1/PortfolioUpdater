using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.ServiceProcess;
using System.Timers;
using System.Configuration;
using PortfolioUpdater.Models;
using System.Threading.Tasks;
using Octokit;
using System.Linq;

namespace PortfolioUpdater
{
    partial class PortfolioUpdater : ServiceBase
    {
        private static readonly string DB_CONNECTION_STRING = ConfigurationManager.ConnectionStrings["databaseConnection"].ConnectionString;
        private static string token = ConfigurationManager.AppSettings["githubToken"];
        private static string username = ConfigurationManager.AppSettings["githubUsername"];
        private static GitHubClient client = new GitHubClient(new ProductHeaderValue("PortfolioUpdater"));

        public PortfolioUpdater()
        {
            InitializeComponent();
            eventLog = new EventLog();
            if (!EventLog.SourceExists("WebAppSource"))
            {
                EventLog.CreateEventSource("WebAppSource", "WebAppServiceLog");
            }
            eventLog.Source = "WebAppSource";
            eventLog.Log = "WebAppServiceLog";
        }

        protected override void OnStart(string[] args)
        {
            eventLog.WriteEntry("PortfolioUpdater Service Started");

            // Set up a timer that triggers every 30 minutes
            Timer timer = new Timer();
            timer.Interval = (60 * 60000);
            timer.Elapsed += new ElapsedEventHandler(this.OnTimer);
            timer.Start();
        }

        protected override void OnStop()
        {
            eventLog.WriteEntry("PortfolioUpdater Service Stopped");
        }

        private async void OnTimer(object sender, ElapsedEventArgs args)
        {
            try
            {
                // Get the names and Ids of all active projects.
                List<MyProject> projects = GetActiveProjects();

                // Authenticate using the token
                var tokenAuth = new Credentials(token);
                client.Credentials = tokenAuth;

                // Use Octokit to check if it has been updated since last update
                foreach (MyProject proj in projects)
                {
                    var repo = await client.Repository.Get(username, proj.ProjectName);
                    // Check if repository has been updated since last import, or has never been imported before
                    if (proj.UpdatedAt == null || repo.UpdatedAt.DateTime >= proj.UpdatedAt)
                    {
                        // Get updated project Content, Languages, RepoUrl, LogoAltText
                        MyProject project = await GetProjectDetails(proj, repo);

                        // Update the project in the db
                        UpdateProject(project);
                    }
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
                eventLog.WriteEntry(ex.ToString());
            }
        }

        public List<MyProject> GetActiveProjects()
        {
            List<MyProject> projects = new List<MyProject>();

            using (SqlConnection connection = new SqlConnection(DB_CONNECTION_STRING))
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.CommandType = CommandType.StoredProcedure;

                    command.CommandText = "sp_GetActiveProjects";
                    command.Connection = connection;
                    connection.Open();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            projects.Add(new MyProject
                            {
                                ProjectId = int.Parse(reader["ProjectId"].ToString()),
                                ProjectName = reader["ProjectName"].ToString(),
                                LogoURL = reader["LogoUrl"].ToString(),
                                ProjectURL = (reader["ProjectUrl"] != DBNull.Value ? reader["ProjectUrl"].ToString() : null),
                                UpdatedAt = reader["UpdatedAt"] == DBNull.Value ? (DateTime?)null : DateTime.Parse(reader["UpdatedAt"].ToString())
                            });
                        }
                    }
                }
            }

            return projects;
        }

        public async Task<MyProject> GetProjectDetails(MyProject project, Repository repo)
        {
            var langs = await client.Repository.GetAllLanguages(username, project.ProjectName);

            // Get a list of language names as strings
            List<string> langNames = new List<string>();
            foreach (RepositoryLanguage lang in langs)
            {
                langNames.Add(lang.Name);
            }

            // Assign the rest of the values to the project
            project.ProjectContent = await client.Repository.Content.GetReadmeHtml(username, project.ProjectName);
            project.ProjectLanguages = langNames;
            project.GithubURL = repo.HtmlUrl;
            project.LogoAltText = project.ProjectName + " logo";
            project.UpdatedAt = DateTime.Now;

            return project;
        }

        public void UpdateProject(MyProject project)
        {
            using (SqlConnection connection = new SqlConnection(DB_CONNECTION_STRING))
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add(new SqlParameter("@ProjectId", project.ProjectId));
                    command.Parameters.Add(new SqlParameter("@ProjectContent", project.ProjectContent));
                    command.Parameters.Add(new SqlParameter("@ProjectLanguages", string.Join(",", project.ProjectLanguages.ToArray())));
                    command.Parameters.Add(new SqlParameter("@LogoAltText", project.LogoAltText));
                    command.Parameters.Add(new SqlParameter("@GithubUrl", project.GithubURL));
                    command.Parameters.Add(new SqlParameter("@UpdatedAt", project.UpdatedAt));

                    command.CommandText = "sp_UpdateProject";
                    command.Connection = connection;
                    connection.Open();

                    command.ExecuteNonQuery();
                }
            }
        }

        public static void LogException(Exception e)
        {
            LogErrorToDatabase(e.Message + " - " + e.InnerException, e.GetType().Name, e.StackTrace);
        }

        public static void LogErrorToDatabase(string message, string errorType, string stackTrace = "Not Available")
        {
            using (SqlConnection connection = new SqlConnection(DB_CONNECTION_STRING))
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add(new SqlParameter("@ExceptionMessage", message));
                    command.Parameters.Add(new SqlParameter("@ExceptionType", errorType));
                    command.Parameters.Add(new SqlParameter("@ExceptionURL", "PortfolioUpdater"));
                    command.Parameters.Add(new SqlParameter("@ExceptionSource", stackTrace));
                    command.Parameters.Add(new SqlParameter("@Username", "PortfolioUpdater"));

                    command.CommandText = "sp_LogException";
                    command.Connection = connection;
                    connection.Open();

                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
