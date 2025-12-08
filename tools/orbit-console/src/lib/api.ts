import type {
  AgentInfo,
  Job,
  PagedResult,
  DashboardStats,
  JobStatus,
  AgentStatus,
} from '@/types/api'

const API_BASE = '/api'

async function fetchApi<T>(
  endpoint: string,
  options?: RequestInit
): Promise<T> {
  const response = await fetch(`${API_BASE}${endpoint}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
  })

  if (!response.ok) {
    const error = await response.text()
    throw new Error(error || `HTTP ${response.status}`)
  }

  return response.json()
}

// Dashboard
export async function getDashboardStats(): Promise<DashboardStats> {
  return fetchApi<DashboardStats>('/dashboard/stats')
}

// Agents
export async function getAgents(params?: {
  status?: AgentStatus
  group?: string
  page?: number
  pageSize?: number
}): Promise<PagedResult<AgentInfo>> {
  const searchParams = new URLSearchParams()
  if (params?.status) searchParams.set('status', params.status)
  if (params?.group) searchParams.set('group', params.group)
  if (params?.page) searchParams.set('page', params.page.toString())
  if (params?.pageSize) searchParams.set('pageSize', params.pageSize.toString())

  const query = searchParams.toString()
  return fetchApi<PagedResult<AgentInfo>>(`/agents${query ? `?${query}` : ''}`)
}

export async function getAgent(agentId: string): Promise<AgentInfo> {
  return fetchApi<AgentInfo>(`/agents/${agentId}`)
}

// Jobs
export async function getJobs(params?: {
  status?: JobStatus
  agentId?: string
  page?: number
  pageSize?: number
}): Promise<PagedResult<Job>> {
  const searchParams = new URLSearchParams()
  if (params?.status) searchParams.set('status', params.status)
  if (params?.agentId) searchParams.set('agentId', params.agentId)
  if (params?.page) searchParams.set('page', params.page.toString())
  if (params?.pageSize) searchParams.set('pageSize', params.pageSize.toString())

  const query = searchParams.toString()
  return fetchApi<PagedResult<Job>>(`/jobs${query ? `?${query}` : ''}`)
}

export async function getJob(jobId: string): Promise<Job> {
  return fetchApi<Job>(`/jobs/${jobId}`)
}

export async function createJob(request: {
  command: string
  payload?: unknown
  priority?: number
  targetAgentId?: string
  targetAgentGroup?: string
  requiredCapabilities?: string[]
  timeout?: number
}): Promise<Job> {
  return fetchApi<Job>('/jobs', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export async function cancelJob(jobId: string, reason?: string): Promise<void> {
  await fetchApi(`/jobs/${jobId}/cancel`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
  })
}

export async function retryJob(jobId: string): Promise<Job> {
  return fetchApi<Job>(`/jobs/${jobId}/retry`, {
    method: 'POST',
  })
}
