# Genesis of the Engine: How a Discord Question Killed File Copying

![The 48ms Breakthrough: Killing File Copying Infographic](../assets/infographic-5-genesis-tylers76.png)

*Illustration / Mood Art:*  
![TylerS76 Genesis Spark Crystal Lattice Art](../assets/art-5-genesis-spark-crystal.png)

**Great tools rarely start in corporate planning meetings. The Valheim Profile Engine started with a casual suggestion in a modding Discord from community member TylerS76.**

---

## The Spark: TylerS76's Discord Insight

```
+-------------------------------------------------------------------------------+
| DISCORD #mod-development                                                      |
|                                                                               |
| [Avatar] TylerS76:                                                            |
| "Maybe something with file linking?... like hardlinks or symlinks"            |
+-------------------------------------------------------------------------------+
                                       |
                                       v
                     The Question That Started It All
```

Every Valheim modder had accepted the pain: to switch from a solo creative world to a 60-mod dedicated server, you sat and watched a progress bar copy 85 megabytes of DLLs and assets.

Tyler asked the obvious question that everyone else had walked past: **Why are we copying files at all?**

---

## The Technical Investigation

Tyler's idea launched an immediate dive into the Windows filesystem architecture:

### Attempt 1: Hardlinks
- **The Concept**: Point multiple directory entries to the same underlying MFT record.
- **The Failure**: NTFS hardlinks only work on **individual files**, never directories. Managing hundreds of individual file hardlinks is slow and brittle.

### Attempt 2: Symbolic Links (`mklink /D`)
- **The Concept**: Standard filesystem pointers across paths and volumes.
- **The Failure**: Windows restricts symbolic links behind `SeCreateSymbolicLinkPrivilege`. Running it required either an annoying UAC Administrator prompt or forcing every user into Windows Developer Mode.

### Attempt 3: NTFS Directory Junctions (`mklink /J`)
- **The Breakthrough**: Junctions use NTFS Directory Reparse Points (tag `0xA0000003`).
- **The Victory**: **Zero administrator privileges required!** Any standard user can create, swap, and delete directory junctions in their own user space instantly.

---

## The Genesis Timeline

| Stage | What Happened |
|---|---|
| **The Friction** | Modders losing minutes per session copying files and hitting lock errors |
| **The Spark** | **TylerS76** suggests exploring filesystem links in Discord |
| **The Dead Ends** | Hardlinks blocked on folders; Symlinks blocked by Windows UAC |
| **The Breakthrough** | NTFS Directory Reparse Points discovered as unprivileged solution |
| **The Engine** | Standalone, open-source PowerShell & JSON engine deployed in 48ms |

---

## The Takeaway

> **"Maybe something with file linking?"**
> A one-line Discord insight killed 12-second file copying forever.
> 
> *Architecture and engine realized by the Deep North team. Sparked by TylerS76.*
