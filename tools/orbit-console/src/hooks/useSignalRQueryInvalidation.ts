import { useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useConnectionStore } from '@/stores/connection'

/**
 * Hook that automatically invalidates React Query cache when SignalR events occur.
 * This ensures the UI stays in sync with real-time updates from the server.
 */
export function useSignalRQueryInvalidation() {
  const queryClient = useQueryClient()
  const { recentAgentEvents, recentJobEvents } = useConnectionStore()

  // Invalidate agent-related queries when agent events occur
  useEffect(() => {
    if (recentAgentEvents.length > 0) {
      const latestEvent = recentAgentEvents[0]

      // Invalidate agent list
      queryClient.invalidateQueries({ queryKey: ['agents'] })

      // Invalidate specific agent
      queryClient.invalidateQueries({ queryKey: ['agent', latestEvent.agentId] })

      // Invalidate dashboard stats
      queryClient.invalidateQueries({ queryKey: ['dashboard', 'stats'] })
    }
  }, [recentAgentEvents, queryClient])

  // Invalidate job-related queries when job events occur
  useEffect(() => {
    if (recentJobEvents.length > 0) {
      const latestEvent = recentJobEvents[0]

      // Invalidate job list
      queryClient.invalidateQueries({ queryKey: ['jobs'] })

      // Invalidate specific job
      queryClient.invalidateQueries({ queryKey: ['job', latestEvent.jobId] })

      // Invalidate dashboard stats
      queryClient.invalidateQueries({ queryKey: ['dashboard', 'stats'] })

      // Invalidate workflow instances (jobs are linked to workflows)
      queryClient.invalidateQueries({ queryKey: ['workflow-instances'] })
    }
  }, [recentJobEvents, queryClient])
}
