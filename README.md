Portfolio Updater is an ASP.Net Windows Service that I created to periodically update each of the projects featured on my portfolio site. It utilises the [Octokit.Net](https://github.com/octokit/octokit.net) library in order to get up to date data from each repoistory.

There is an MSSQL backend with a table with each project I want to list on my portfolio. From there it will check each project against the Github repository to see if the repository has been updated since last import. If so, it will get the ReadMe files html contents, the languages used, and the repository url.

I wanted to create a service so that I would not have to manually update both the ReadMe for the projects repository, but then also update my portfolio. This allows me to simply update the ReadMe with a simple description of the project, and any other important information, and have my porfolio site be updated roughly an hour later.
