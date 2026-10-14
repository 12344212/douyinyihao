Select-String -Path "D:\UnitProject\26-10-8\Assets\Scripts\Tutorial\TutorialOverlay.cs" -Pattern "timeScale|deltaTime|unscaled|WaitFor|Update" | ForEach-Object { "$($_.LineNumber):$($_.Line)" }
