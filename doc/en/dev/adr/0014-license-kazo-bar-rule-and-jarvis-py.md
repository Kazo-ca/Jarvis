# ADR 0014: Apache 2.0 license, rule on Kazo Bar and removal of `jarvis.py`

## Status

Accepted by Benoit on 2026-11-19 (answers given in the conversation about the design document).

- **Clarifies:** ADRs 0001 to 0004, which name Kazo Bar as a model. Their decisions do not change.
- **Changes:** section I of `AGENTS.md` (rule 3) and the [design document](../plans/design.md)
  (decision 36, assumptions 4, 7 and 8).

## Context

- The design document said "from Kazo Bar we keep the hosts" and "taken from Kazo Bar". Those words could
  be read as a dependency on the `Kazo-ca/bar` repository.
- Benoit's answers of 2026-11-19: "We do not use Kazo Bar. We do as it does." then "We may copy Kazo Bar's
  code, but we do not reference it."
- Assumption 7 of the design said "same license as Kazo Bar". Kazo Bar has no license file. Benoit's
  correction of 2026-11-19: "The repository is public and under the same license as OpenJarvis."
  OpenJarvis is under the Apache 2.0 license.
- Assumption 8 planned that `jarvis.py`, Benoit's former voice assistant, would keep running until the end of
  stage 2. Benoit's correction of 2026-11-19: "`jarvis.py` is replaced and is no longer used."

## Decision

### 1. Kazo Bar: copying allowed, no reference

1. Kazo.Jarvis never references Kazo Bar: no project reference, no package, no Git submodule, no file linked
   from another repository.
2. Kazo Bar's code may be copied into this repository where it fits. The copy is adapted to the rules here:
   vertical slices (ADR 0002), `Kazo.Jarvis.*` names, tests at the same path.
3. Copied code becomes Kazo.Jarvis code. It evolves separately and nothing resynchronizes it with Kazo Bar.
4. No Jarvis module is added to Kazo Bar.
5. In the docs, Kazo Bar is named as a model ("as in Kazo Bar", "modeled on"). We no longer write "taken
   from" or "we keep from", which suggest a dependency.

### 2. Apache 2.0 license

1. The repository is under the Apache 2.0 license, the same as OpenJarvis. The `LICENSE` file is at the root.
2. The repository is meant to be public. Changing its visibility on GitHub is Benoit's to do.
3. The licenses of third-party components are still to be settled **before** a package is distributed
   publicly: espeak-ng (GPLv3, in sherpa-onnx 1.13.8), openWakeWord models (CC BY-NC-SA 4.0) and the license
   of each voice (ADR 0006). This ADR does not settle them.

### 3. `jarvis.py` is retired

1. `jarvis.py` (`C:\jarvis\jarvis.py`, outside the repository) is no longer used. There is no transition
   period.
2. It is neither a behavioral reference nor a dependency. The reference remains OpenJarvis (`AGENTS.md`).
3. Nothing in Kazo.Jarvis reads its configuration or its files.

## Consequences

- The architecture tests can check that no project references a `Kazo.Bar.*` assembly. That check is added
  in work item `stage1/2-kernel` or the next one that touches those tests.
- A fix made in Kazo Bar does not reach Kazo.Jarvis by itself: it has to be copied by hand.
- While `jarvis.py` is no longer used and stage 1 is not delivered, Benoit has no voice assistant in service.
- A public repository exposes its history: no secret may be in it (`AGENTS.md`, section II, rule 5).
