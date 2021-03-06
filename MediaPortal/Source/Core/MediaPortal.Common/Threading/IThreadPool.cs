#region Copyright (C) 2007-2011 Team MediaPortal

/*
    Copyright (C) 2007-2011 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System.Threading;

namespace MediaPortal.Common.Threading
{
  #region Enums

  public enum QueuePriority
  {
    Low,
    Normal,
    High
  }

  #endregion

  /// <summary>
  /// Container and management service for asynchronous execution threads and tasks.
  /// </summary>
  /// <remarks>
  /// Use this service to create simple (low-level) work tasks. Use the <see cref="TaskScheduler.ITaskScheduler"/> service to manage higher-level
  /// persistent tasks.
  /// </remarks>
  public interface IThreadPool
  {
    #region Methods to add work

    IWork Add(DoWorkHandler work);
    IWork Add(DoWorkHandler work, QueuePriority queuePriority);
    IWork Add(DoWorkHandler work, string description);
    IWork Add(DoWorkHandler work, ThreadPriority threadPriority);
    IWork Add(DoWorkHandler work, WorkEventHandler workCompletedHandler);
    IWork Add(DoWorkHandler work, string description, QueuePriority queuePriority);
    IWork Add(DoWorkHandler work, string description, QueuePriority queuePriority, ThreadPriority threadPriority);
    IWork Add(DoWorkHandler work, string description, QueuePriority queuePriority, ThreadPriority threadPriority, WorkEventHandler workCompletedHandler);
    void Add(IWork work);
    void Add(IWork work, QueuePriority queuePriority);

    #endregion

    #region Methods to manage interval-based work

    void AddIntervalWork(IIntervalWork intervalWork, bool runNow);
    void RemoveIntervalWork(IIntervalWork intervalWork);

    #endregion

    #region Methods to control the threadpool

    void Stop();

    #endregion

    #region Threadpool status properties

    int ThreadCount { get; }
    int BusyThreadCount { get; }
    long WorkItemsProcessed { get; }
    int QueueLength { get; }
    int MinimumThreads { get; }
    int MaximumThreads { get; }

    #endregion
  }
}
