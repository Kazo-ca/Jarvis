// Takes the screenshots of this folder from the Web host (simulated services, no model, no microphone).
// 1. dotnet run --project src/Kazo.Jarvis.Web
// 2. node design/screenshots/capture.cjs http://localhost:5180
// The host keeps its state: run it against a host that was just started.
const { chromium } = require('playwright');
const path = require('node:path');

const base = process.argv[2] ?? 'http://localhost:5180';
const shot = name => path.join(__dirname, name + '.png');
const press = (page, id) => page.locator(id).dispatchEvent('click');

(async () => {
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1600, height: 1000 }, deviceScaleFactor: 2 });
await page.goto(base);
await page.locator('#jarvis-root[data-interactive="true"]').waitFor();
const orb = page.locator('.orb-frame .orb');
const view = page.locator("[data-dialog-key='Projects.View']");
const settings = page.locator("[data-dialog-key='Settings.Window']");

// The orb at rest.
await page.locator('.orb-frame').screenshot({ path: shot('orb') });

// Four sessions started by hand ended in the same folder: the subject takes a lot of room in « Général ».
for (let ended = 0; ended < 4; ended++) {
  await press(page, '#hand-session-work');
  await press(page, '#hand-session-end');
}

// A fifth one works in that folder, starts a sub-agent and waits for a permission.
await press(page, '#hand-session-work');
await press(page, '#hand-session-subtask');
await press(page, '#hand-session-wait');

// The task fan (right click on the orb). The buttons of the test bench are hidden for the picture.
await page.addStyleTag({ content: '.host-controls { visibility: hidden; }' });
await orb.click({ button: 'right' });
await page.locator('[data-fan][data-fan-phase="Open"]').waitFor();
await page.waitForTimeout(600);
await page.screenshot({ path: shot('task-fan') });
await page.locator('[data-fan]').press('Escape');
await page.locator('[data-fan]').waitFor({ state: 'detached' });

// The projects view (double click on the orb): all the projects, with the folder that has no project yet.
await orb.dblclick();
await view.locator('[data-projects-all]').waitFor();
await view.screenshot({ path: shot('projects-all') });

// « Général » offers to make a project of the subject (ADR 0026, section 6).
await view.locator("[data-rail='_general']").click();
await view.locator('[data-offer]').waitFor();
await view.screenshot({ path: shot('projects-offer') });
await view.locator("[data-rail='_all']").click();

// The folder gets its project; the view shows the project, its subject, the band of tasks and the task thread.
await view.locator('[data-folder-card]').click();
const request = settings.locator('[data-project-request]');
await request.locator('[data-project-request-name]').fill('Kazo Bar');
await request.locator('[data-project-request-create]').click();
await request.waitFor({ state: 'detached' });
await settings.screenshot({ path: shot('projects-page') });
// Claude Code may work in the folder of the project: needed further down for a session Jarvis starts itself.
const entry = settings.locator("[data-project='kazo-bar']");
await entry.locator('[data-project-allow]').click();
await entry.locator('[data-project-allow-confirm]').click();
await settings.locator("[data-project='kazo-bar'][data-project-allowed='true']").waitFor();
await settings.locator('.dialog-close').click();
await view.locator("[data-rail='kazo-bar']").click();
// A message written in the thread of the subject: the agent of the project answers. Jarvis takes a text only
// at rest, and the ends of the sessions are announced first.
await page.locator('#jarvis-root[data-phase="Idle"]').waitFor({ timeout: 60000 });
await view.locator('[data-thread-text]').fill('où en est le pied de page ?');
await view.locator('[data-thread-send]').click();
await view.locator("[data-thread-message='answer'][data-thread-pending='false']").waitFor();
await view.locator('[data-step][data-step-kind="Active"]').first().click();
await view.locator('[data-task-thread]').waitFor();
await view.screenshot({ path: shot('projects-subject') });

// What waits for the user, all projects together.
await view.locator("[data-rail='_attention']").click();
await view.locator('[data-attention]').waitFor();
await view.screenshot({ path: shot('projects-attention') });

// A work handed to Claude Code from the thread of the subject: the thread of its task lists the file written
// and unfolds its full log (ADR 0026, section 5).
await view.locator("[data-rail='kazo-bar']").click();
await page.locator('#jarvis-root[data-phase="Idle"]').waitFor({ timeout: 60000 });
await view.locator('[data-thread-text]').fill('Ask Claude Code to write the notes');
await view.locator('[data-thread-send]').click();
const confirmation = page.locator("[data-dialog-key='Permissions.Confirmation']");
await confirmation.locator('[data-confirmation-approve]').click({ timeout: 60000 });
const written = view.locator('[data-step][data-step-kind="Done"]').filter({ hasText: 'write the notes' });
await written.click({ timeout: 60000 });
await view.locator('[data-task-log-toggle]').click();
await view.locator("[data-task-log] [data-log-kind='Ended']").waitFor();
await page.locator('#jarvis-root[data-phase="Idle"]').waitFor({ timeout: 60000 });
await view.screenshot({ path: shot('projects-task-log') });

await browser.close();
})();
