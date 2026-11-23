# Screenshots

Screenshots of the application **as it is built**, not of the prototypes. They are taken on the Web host
(`src/Kazo.Jarvis.Web`), whose services are simulated: no model, no microphone, no real Claude Code session.
The root readmes show them. Take them again when a work item changes what they show.

```
dotnet run --project src/Kazo.Jarvis.Web
node design/screenshots/capture.cjs http://localhost:5180
```

Run the script against a host that was just started: the host keeps its tasks and its projects.

| File | What it shows |
| --- | --- |
| [orb.png](orb.png) | The orb at rest |
| [task-fan.png](task-fan.png) | Task fan: a session that waits for a permission, in a folder with no project |
| [projects-all.png](projects-all.png) | Projects view, "All projects", with the card of a folder with no project |
| [projects-subject.png](projects-subject.png) | Projects view: a project, its subject, the band of tasks, the thread of a task |
| [projects-attention.png](projects-attention.png) | Projects view, "To handle" |
| [projects-page.png](projects-page.png) | "Projects" page of the settings |
