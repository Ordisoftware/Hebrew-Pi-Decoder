﻿/// <license>
/// This file is part of Ordisoftware Hebrew Pi.
/// Copyright 2025 Olivier Rogier.
/// See www.ordisoftware.com for more information.
/// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
/// If a copy of the MPL was not distributed with this file, You can obtain one at
/// https://mozilla.org/MPL/2.0/.
/// If it is not possible or desirable to put the notice in a particular file,
/// then You may include the notice in a location(such as a LICENSE file in a
/// relevant directory) where a recipient would be likely to look for such a notice.
/// You may add additional accurate notices of copyright ownership.
/// </license>
/// <created> 2025-01-13 </created>
/// <edited> 2025-01-14 </edited>
namespace Ordisoftware.Hebrew.Pi;

/// <summary>
/// Provides application's main form.
/// </summary>
/// <seealso cref="T:System.Windows.Forms.Form"/>
public partial class MainForm
{

  private async Task DoActionBatchRun(long startingIterationNumber)
  {
    TestCounter = 10;
    if ( startingIterationNumber == 0 )
    {
      DB.DropTable<IterationRow>();
      DB.CreateTable<IterationRow>();
    }
    bool firstIteration = true;
    long countPrevious = 0;
    long countCurrent = 0;
    long iteration = 0;
    while ( true )
    {
      if ( Globals.CancelRequired ) break;
      UpdateStatusProgress(string.Format(IterationText, iteration, "?"));
      UpdateStatusInfo(CountingText);
      countCurrent = GetRepeatingCount().Result;
      UpdateStatusProgress(string.Format(IterationText, iteration, countCurrent));
      UpdateStatusInfo(CountedText);
      DB.Insert(new IterationRow(countCurrent, DateTime.Now));
      if ( Globals.CancelRequired ) break;
      if ( countCurrent == 0 )
      {
        DisplayManager.Show(string.Format(NoRepeatedText, iteration));
        break;
      }
      if ( !firstIteration )
        if ( countPrevious > countCurrent )
        {
          if ( !DisplayManager.QueryYesNo(string.Format(AskStartNextIfMore, iteration, countPrevious, countCurrent)) )
            Globals.CancelRequired = true;
        }
        else
        {
          if ( !DisplayManager.QueryYesNo(string.Format(AskStartNextIfLess, iteration, countPrevious, countCurrent)) )
            Globals.CancelRequired = true;
        }
      countPrevious = countCurrent;
      firstIteration = false;
      UpdateStatusInfo(UpdatingText);
      AddPositionToMotifs();
      if ( Globals.CancelRequired ) break;
      iteration++;
    }
    if ( Globals.CancelRequired )
      UpdateStatusInfo(CanceledText);
    else
      UpdateStatusInfo(FinishedText);
  }

  int TestCounter = 10;

  private async Task<int> GetRepeatingCount()
  {
    await Task.Delay(1000);
    return TestCounter--;
    try
    {
      var query = @"SELECT COUNT(DISTINCT Motif) AS UniqueRepeated
                  FROM Decuplets
                  WHERE Motif IN (
                    SELECT Motif
                    FROM Decuplets
                    GROUP BY Motif
                    HAVING COUNT(Motif) > 1
                  );";

      return DB.Query<int>(query).Single();
    }
    catch ( Exception ex )
    {
      // TODO manage
      throw;
    }
  }

  private async void AddPositionToMotifs()
  {
    return;
    var query = @"UPDATE Decuplets
                  SET Motif = Motif + Position
                  WHERE Motif IN (
                    SELECT Motif
                    FROM Decuplets
                    GROUP BY Motif
                    HAVING COUNT(*) > 1
                  );";
    DB.Execute(query);
  }

}
