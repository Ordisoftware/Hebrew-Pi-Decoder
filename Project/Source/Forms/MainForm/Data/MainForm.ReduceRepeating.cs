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
/// <created> 2025-01 </created>
/// <edited> 2025-01 </edited>
namespace Ordisoftware.Hebrew.Pi;

//using SQLHelper = SQLHelper_NoTemp_InMotif;
//using SQLHelper = SQLHelper_WithTemp_InPos;
using SQLHelper = SQLHelper_WithTemp_InPos_PK;
//using SQLHelper = SQLHelper_WithTemp_InMotif;
//using SQLHelper = SQLHelper_WithTemp_InMotif_PK;

/// <summary>
/// Provides application's main form.
/// </summary>
/// <seealso cref="T:System.Windows.Forms.Form"/>
partial class MainForm
{

  enum IteratingStep { Next, Counting, Adding }

  private long IterationAllRepeatingCount;
  //private long RepeatingAddedCount;

  private async void WriteLog(string str)
  {
    EditLog.Invoke(() => EditLog.AppendText(str));
  }

  private async void WriteLogLine(string str = "")
  {
    WriteLog(str + Globals.NL);
  }

  private async void WriteLogTime(bool isSubBatch)
  {
    WriteLogLine(( isSubBatch ? "    " : string.Empty ) +
                 Operation.ToString() + ": " +
                 Globals.ChronoSubBatch.Elapsed.AsReadable());
  }

  private async Task DoActionReduceRepeatingAsync()
  {
    IteratingStep iteratingStep = IteratingStep.Next;
    var table = DB.Table<IterationRow>().ToList();
    IterationRow lastRow = table.LastOrDefault();
    IterationRow row;
    ReduceRepeatingIteration = 0;
    IterationAllRepeatingCount = 1;
    long countPrevious = 0;
    bool hasError = false;
    try
    {
      // Prepare
      if ( lastRow is not null )
      {
        ReduceRepeatingIteration = lastRow.Iteration;
        if ( lastRow.ElapsedCounting is null )
          iteratingStep = IteratingStep.Counting;
        else
        if ( lastRow.ElapsedAdding is null )
        {
          lastRow.MaxOccurences = null;
          lastRow.AllRepeatingCount = null;
          lastRow.UniqueRepeatingCount = null;
          lastRow.RemainingRate = null;
          lastRow.RepeatingRate = null;
          lastRow.ElapsedCounting = null;
          iteratingStep = IteratingStep.Counting;
          DB.Update(lastRow);
          LoadIterationGrid();
        }
        else
        if ( lastRow.UniqueRepeatingCount == 0 && lastRow.RemainingRate == 0 )
          return;
        else
        {
          countPrevious = (long)lastRow.UniqueRepeatingCount;
          ReduceRepeatingIteration++;
        }
      }
      EditLog.Invoke(EditLog.Clear);
      Processing = ProcessingType.ReduceRepeating;
      // Count all rows
      long countRows = 0;
      if ( SelectCountAllRows.Checked )
      {
        Globals.ChronoBatch.Restart();
        Operation = OperationType.CountingAllRows;
        Globals.ChronoSubBatch.Restart();
        countRows = DB.ExecuteScalar<long>($"SELECT COUNT(*) FROM [{DecupletRow.TableName}]");
        Globals.ChronoSubBatch.Stop();
        WriteLogTime(false);
        WriteLogLine();
        if ( !CheckIfBatchCanContinueAsync().Result ) return;
        Operation = OperationType.CountedAllRows;
      }
      else
      {
        Globals.ChronoBatch.Restart();
        countRows = (long)EditMaxMotifs.Value;
      }
      // Loop
      for ( ; IterationAllRepeatingCount > 0; ReduceRepeatingIteration++ )
      {
        EditLog.Invoke(() => EditLog.AppendText("ITERATION #" + ReduceRepeatingIteration + Globals.NL));
        // Init current row
        if ( !CheckIfBatchCanContinueAsync().Result ) break;
        if ( iteratingStep == IteratingStep.Next )
        {
          row = new IterationRow { Iteration = ReduceRepeatingIteration };
          DB.Insert(row);
        }
        else
        {
          row = lastRow;
          if ( ReduceRepeatingIteration > 0 )
          {
            lastRow = table[table.Count - 2];
            countPrevious = (long)lastRow.AllRepeatingCount;
          }
          if ( iteratingStep == IteratingStep.Adding )
            IterationAllRepeatingCount = (long)row.AllRepeatingCount;
        }
        LoadIterationGrid();
        // Count repeating motifs
        if ( !CheckIfBatchCanContinueAsync().Result ) break;
        if ( iteratingStep == IteratingStep.Next || iteratingStep == IteratingStep.Counting )
        {
          Globals.ChronoSubBatch.Restart();
          // Grouping unique repeating
          Operation = OperationType.Grouping;
          SQLHelper.CreateUniqueRepeatingMotifsTempTable(DB);
          WriteLogTime(true);
          if ( !CheckIfBatchCanContinueAsync().Result ) break;
          Operation = OperationType.Grouped;
          // Counting unique repeating
          Operation = OperationType.CountingUniqueRepeating;
          var list = SQLHelper.GetUniqueRepeatingStats(DB);
          WriteLogTime(true);
          if ( !CheckIfBatchCanContinueAsync().Result ) break;
          Operation = OperationType.CountedUniqueRepeating;
          row.MaxOccurences = list[0].MaxOccurrences;
          row.UniqueRepeatingCount = list[0].CountMotifs;
          //Degrouping all repeating
          Operation = OperationType.Degrouping;
          SQLHelper.CreateAllRepeatingMotifsTempTable(DB);
          WriteLogTime(true);
          if ( !CheckIfBatchCanContinueAsync().Result ) break;
          Operation = OperationType.Degrouped;
          // Counting all repeating
          Operation = OperationType.CountingAllRepeating;
          IterationAllRepeatingCount = SQLHelper.CountAllRepeatingMotifs(DB);
          Globals.ChronoSubBatch.Stop();
          WriteLogTime(true);
          if ( !CheckIfBatchCanContinueAsync().Result ) break;
          Operation = OperationType.CountedAllRepeating;
          row.AllRepeatingCount = IterationAllRepeatingCount;
          // Calculate rates and update row
          row.RepeatingRate = row.AllRepeatingCount == 0
            ? 0
            : row.AllRepeatingCount == row.UniqueRepeatingCount
              ? 100
              : Math.Round((double)row.AllRepeatingCount * 100 / countRows, 2);
          row.RemainingRate = row.UniqueRepeatingCount == 0
              ? 0
              : ReduceRepeatingIteration == 0
                ? 100
                : Math.Round((double)row.UniqueRepeatingCount * 100 / countPrevious, 2);
          row.ElapsedCounting = Globals.ChronoSubBatch.Elapsed;
          DB.Update(row);
          LoadIterationGrid();
          if ( ReduceRepeatingIteration > 0 && IterationAllRepeatingCount > countPrevious
            && !DisplayManager.QueryYesNo(string.Format(AppTranslations.AskStartNextIfMore,
                                                        ReduceRepeatingIteration,
                                                        countPrevious,
                                                        IterationAllRepeatingCount)) )
          {
            Globals.CancelRequired = true;
            break;
          }
          if ( iteratingStep != IteratingStep.Next )
            iteratingStep = IteratingStep.Next;
        }
        countPrevious = IterationAllRepeatingCount;
        // Add position to repeating motifs
        if ( !CheckIfBatchCanContinueAsync().Result ) break;
        if ( iteratingStep == IteratingStep.Next || iteratingStep == IteratingStep.Adding )
        {
          if ( IterationAllRepeatingCount > 0 )
          {
            Operation = OperationType.Adding;
            Globals.ChronoSubBatch.Restart();
            long count = SQLHelper.AddPositionToRepeatingMotifs(DB);
            Globals.ChronoSubBatch.Stop();
            WriteLogTime(false);
            WriteLogLine();
            if ( !CheckIfBatchCanContinueAsync().Result ) break;
            if ( row.AllRepeatingCount != count )
            {
              WriteLogLine("Counted: " + row.AllRepeatingCount);
              WriteLogLine("Added: " + count);
              WriteLogLine();
            }
            Operation = OperationType.Added;
            row.ElapsedAdding = Globals.ChronoSubBatch.Elapsed;
            DB.Update(row);
            LoadIterationGrid();
          }
          else
          {
            row.AllRepeatingCount = 0;
            row.RepeatingRate = 0;
            row.ElapsedAdding = TimeSpan.Zero;
            DB.Update(row);
            LoadIterationGrid();
          }
          if ( iteratingStep != IteratingStep.Next )
            iteratingStep = IteratingStep.Next;
        }
        if ( !EditNormalizeAutoLoop.Checked ) break;
      }
    }
    catch ( Exception ex )
    {
      Processing = ProcessingType.Error;
      hasError = true;
      Except = ex;
      ex.Manage();
    }
    finally
    {
      if ( !hasError )
        if ( Globals.CancelRequired )
          Processing = ProcessingType.Canceled;
        else
          Processing = ProcessingType.Finished;
    }
  }

}
