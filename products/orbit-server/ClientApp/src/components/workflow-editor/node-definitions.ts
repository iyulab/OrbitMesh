import type { NodeDefinition, WorkflowNodeType, NodeCategory } from './types'

/**
 * Node definitions for all available workflow node types.
 * Each definition includes metadata, default configuration, and configuration schema.
 */
export const NODE_DEFINITIONS: Record<WorkflowNodeType, NodeDefinition> = {
  // ============================================
  // TRIGGER NODES - Entry points for workflows
  // ============================================
  'trigger-manual': {
    type: 'trigger-manual',
    category: 'trigger',
    label: 'Manual Trigger',
    description: 'Start workflow manually with a button click',
    icon: 'Play',
    color: '#22c55e',
    defaultConfig: {
      buttonLabel: 'Run Workflow',
    },
    configSchema: [
      {
        key: 'buttonLabel',
        label: 'Button Label',
        type: 'text',
        placeholder: 'Enter button text',
        defaultValue: 'Run Workflow',
        description: 'Text displayed on the trigger button',
      },
    ],
  },

  'trigger-schedule': {
    type: 'trigger-schedule',
    category: 'trigger',
    label: 'Schedule Trigger',
    description: 'Start workflow on a recurring schedule',
    icon: 'Clock',
    color: '#22c55e',
    defaultConfig: {
      cronExpression: '0 * * * *',
      timezone: 'UTC',
      enabled: true,
    },
    configSchema: [
      {
        key: 'cronExpression',
        label: 'Schedule (Cron)',
        type: 'cron',
        placeholder: '0 * * * *',
        required: true,
        defaultValue: '0 * * * *',
        description: 'Cron expression for scheduling (e.g., "0 9 * * 1-5" for weekdays at 9 AM)',
      },
      {
        key: 'timezone',
        label: 'Timezone',
        type: 'select',
        defaultValue: 'UTC',
        options: [
          { value: 'UTC', label: 'UTC' },
          { value: 'America/New_York', label: 'Eastern Time (US)' },
          { value: 'America/Los_Angeles', label: 'Pacific Time (US)' },
          { value: 'Europe/London', label: 'London (UK)' },
          { value: 'Europe/Paris', label: 'Paris (EU)' },
          { value: 'Asia/Tokyo', label: 'Tokyo (Japan)' },
          { value: 'Asia/Seoul', label: 'Seoul (Korea)' },
          { value: 'Asia/Shanghai', label: 'Shanghai (China)' },
        ],
        description: 'Timezone for the schedule',
      },
      {
        key: 'enabled',
        label: 'Enabled',
        type: 'boolean',
        defaultValue: true,
        description: 'Whether the schedule is active',
      },
    ],
  },

  'trigger-webhook': {
    type: 'trigger-webhook',
    category: 'trigger',
    label: 'Webhook Trigger',
    description: 'Start workflow when receiving HTTP requests',
    icon: 'Webhook',
    color: '#22c55e',
    defaultConfig: {
      method: 'POST',
      path: '/webhook',
      authentication: 'none',
    },
    configSchema: [
      {
        key: 'path',
        label: 'Webhook Path',
        type: 'text',
        placeholder: '/webhook/my-endpoint',
        required: true,
        defaultValue: '/webhook',
        description: 'URL path for the webhook endpoint',
      },
      {
        key: 'method',
        label: 'HTTP Method',
        type: 'select',
        defaultValue: 'POST',
        options: [
          { value: 'GET', label: 'GET' },
          { value: 'POST', label: 'POST' },
          { value: 'PUT', label: 'PUT' },
          { value: 'PATCH', label: 'PATCH' },
          { value: 'DELETE', label: 'DELETE' },
        ],
        description: 'HTTP method to accept',
      },
      {
        key: 'authentication',
        label: 'Authentication',
        type: 'select',
        defaultValue: 'none',
        options: [
          { value: 'none', label: 'No Authentication' },
          { value: 'basic', label: 'Basic Auth' },
          { value: 'bearer', label: 'Bearer Token' },
          { value: 'header', label: 'Custom Header' },
        ],
        description: 'Authentication method for incoming requests',
      },
      {
        key: 'secretKey',
        label: 'Secret Key',
        type: 'text',
        placeholder: 'Enter secret key',
        description: 'Secret key for authentication (if applicable)',
      },
    ],
  },

  // ============================================
  // ACTION NODES - Perform operations
  // ============================================
  'action-job': {
    type: 'action-job',
    category: 'action',
    label: 'Execute Job',
    description: 'Execute a job on connected agents',
    icon: 'Zap',
    color: '#3b82f6',
    defaultConfig: {
      jobType: '',
      agentSelector: 'any',
      timeout: 300,
      retryCount: 0,
    },
    configSchema: [
      {
        key: 'jobType',
        label: 'Job Type',
        type: 'text',
        placeholder: 'e.g., backup, sync, process',
        required: true,
        description: 'Type of job to execute',
      },
      {
        key: 'agentSelector',
        label: 'Agent Selection',
        type: 'select',
        defaultValue: 'any',
        options: [
          { value: 'any', label: 'Any Available Agent' },
          { value: 'all', label: 'All Agents' },
          { value: 'specific', label: 'Specific Agent' },
          { value: 'tag', label: 'By Tag' },
        ],
        description: 'How to select agents for job execution',
      },
      {
        key: 'agentId',
        label: 'Agent ID',
        type: 'text',
        placeholder: 'Enter agent ID',
        description: 'Specific agent ID (when "Specific Agent" is selected)',
      },
      {
        key: 'agentTag',
        label: 'Agent Tag',
        type: 'text',
        placeholder: 'e.g., production, backup-node',
        description: 'Agent tag to filter by (when "By Tag" is selected)',
      },
      {
        key: 'payload',
        label: 'Job Payload',
        type: 'json',
        placeholder: '{ "key": "value" }',
        description: 'JSON payload to send with the job',
      },
      {
        key: 'timeout',
        label: 'Timeout (seconds)',
        type: 'number',
        defaultValue: 300,
        validation: { min: 1, max: 86400 },
        description: 'Maximum time to wait for job completion',
      },
      {
        key: 'retryCount',
        label: 'Retry Count',
        type: 'number',
        defaultValue: 0,
        validation: { min: 0, max: 10 },
        description: 'Number of times to retry on failure',
      },
    ],
  },

  'action-http': {
    type: 'action-http',
    category: 'action',
    label: 'HTTP Request',
    description: 'Make HTTP requests to external APIs',
    icon: 'Globe',
    color: '#3b82f6',
    defaultConfig: {
      method: 'GET',
      url: '',
      headers: {},
      timeout: 30,
    },
    configSchema: [
      {
        key: 'url',
        label: 'URL',
        type: 'text',
        placeholder: 'https://api.example.com/endpoint',
        required: true,
        description: 'The URL to send the request to',
      },
      {
        key: 'method',
        label: 'HTTP Method',
        type: 'select',
        defaultValue: 'GET',
        options: [
          { value: 'GET', label: 'GET' },
          { value: 'POST', label: 'POST' },
          { value: 'PUT', label: 'PUT' },
          { value: 'PATCH', label: 'PATCH' },
          { value: 'DELETE', label: 'DELETE' },
        ],
        description: 'HTTP method to use',
      },
      {
        key: 'headers',
        label: 'Headers',
        type: 'json',
        placeholder: '{ "Content-Type": "application/json" }',
        defaultValue: {},
        description: 'HTTP headers to include',
      },
      {
        key: 'body',
        label: 'Request Body',
        type: 'json',
        placeholder: '{ "data": "value" }',
        description: 'Request body (for POST, PUT, PATCH)',
      },
      {
        key: 'timeout',
        label: 'Timeout (seconds)',
        type: 'number',
        defaultValue: 30,
        validation: { min: 1, max: 300 },
        description: 'Request timeout in seconds',
      },
      {
        key: 'followRedirects',
        label: 'Follow Redirects',
        type: 'boolean',
        defaultValue: true,
        description: 'Whether to follow HTTP redirects',
      },
    ],
  },

  'action-delay': {
    type: 'action-delay',
    category: 'action',
    label: 'Delay',
    description: 'Wait for a specified duration',
    icon: 'Timer',
    color: '#3b82f6',
    defaultConfig: {
      duration: 5,
      unit: 'seconds',
    },
    configSchema: [
      {
        key: 'duration',
        label: 'Duration',
        type: 'number',
        required: true,
        defaultValue: 5,
        validation: { min: 1, max: 86400 },
        description: 'How long to wait',
      },
      {
        key: 'unit',
        label: 'Time Unit',
        type: 'select',
        defaultValue: 'seconds',
        options: [
          { value: 'seconds', label: 'Seconds' },
          { value: 'minutes', label: 'Minutes' },
          { value: 'hours', label: 'Hours' },
        ],
        description: 'Unit of time for the duration',
      },
    ],
  },

  // ============================================
  // LOGIC NODES - Control flow
  // ============================================
  'logic-condition': {
    type: 'logic-condition',
    category: 'logic',
    label: 'If/Else',
    description: 'Branch workflow based on conditions',
    icon: 'GitBranch',
    color: '#f59e0b',
    defaultConfig: {
      conditions: [],
      combineWith: 'and',
    },
    configSchema: [
      {
        key: 'field',
        label: 'Field to Check',
        type: 'text',
        placeholder: 'e.g., data.status, response.code',
        required: true,
        description: 'The field path to evaluate',
      },
      {
        key: 'operator',
        label: 'Operator',
        type: 'select',
        defaultValue: 'equals',
        options: [
          { value: 'equals', label: 'Equals' },
          { value: 'notEquals', label: 'Not Equals' },
          { value: 'contains', label: 'Contains' },
          { value: 'startsWith', label: 'Starts With' },
          { value: 'endsWith', label: 'Ends With' },
          { value: 'greaterThan', label: 'Greater Than' },
          { value: 'lessThan', label: 'Less Than' },
          { value: 'isEmpty', label: 'Is Empty' },
          { value: 'isNotEmpty', label: 'Is Not Empty' },
          { value: 'regex', label: 'Matches Regex' },
        ],
        description: 'Comparison operator',
      },
      {
        key: 'value',
        label: 'Value',
        type: 'text',
        placeholder: 'Value to compare against',
        description: 'The value to compare with',
      },
      {
        key: 'combineWith',
        label: 'Combine Conditions',
        type: 'select',
        defaultValue: 'and',
        options: [
          { value: 'and', label: 'AND - All must match' },
          { value: 'or', label: 'OR - Any must match' },
        ],
        description: 'How to combine multiple conditions',
      },
    ],
  },

  'logic-switch': {
    type: 'logic-switch',
    category: 'logic',
    label: 'Switch',
    description: 'Route to different paths based on value',
    icon: 'GitMerge',
    color: '#f59e0b',
    defaultConfig: {
      field: '',
      cases: [],
      defaultCase: true,
    },
    configSchema: [
      {
        key: 'field',
        label: 'Field to Switch On',
        type: 'text',
        placeholder: 'e.g., data.type, response.status',
        required: true,
        description: 'The field path to evaluate',
      },
      {
        key: 'cases',
        label: 'Cases',
        type: 'json',
        placeholder: '[{ "value": "case1", "label": "Case 1" }]',
        defaultValue: [],
        description: 'Array of case values and labels',
      },
      {
        key: 'defaultCase',
        label: 'Include Default Case',
        type: 'boolean',
        defaultValue: true,
        description: 'Whether to include a default/fallback path',
      },
    ],
  },

  'logic-loop': {
    type: 'logic-loop',
    category: 'logic',
    label: 'Loop',
    description: 'Iterate over items in an array',
    icon: 'Repeat',
    color: '#f59e0b',
    defaultConfig: {
      arrayField: '',
      batchSize: 1,
      parallelExecution: false,
    },
    configSchema: [
      {
        key: 'arrayField',
        label: 'Array Field',
        type: 'text',
        placeholder: 'e.g., data.items, response.results',
        required: true,
        description: 'The field path containing the array to iterate',
      },
      {
        key: 'batchSize',
        label: 'Batch Size',
        type: 'number',
        defaultValue: 1,
        validation: { min: 1, max: 100 },
        description: 'Number of items to process per iteration',
      },
      {
        key: 'parallelExecution',
        label: 'Parallel Execution',
        type: 'boolean',
        defaultValue: false,
        description: 'Process items in parallel instead of sequentially',
      },
      {
        key: 'maxIterations',
        label: 'Max Iterations',
        type: 'number',
        defaultValue: 1000,
        validation: { min: 1, max: 10000 },
        description: 'Maximum number of iterations (safety limit)',
      },
    ],
  },

  // ============================================
  // TRANSFORM NODES - Data manipulation
  // ============================================
  'transform-filter': {
    type: 'transform-filter',
    category: 'transform',
    label: 'Filter',
    description: 'Filter items based on conditions',
    icon: 'Filter',
    color: '#8b5cf6',
    defaultConfig: {
      conditions: [],
      combineWith: 'and',
    },
    configSchema: [
      {
        key: 'arrayField',
        label: 'Array to Filter',
        type: 'text',
        placeholder: 'e.g., data.items',
        required: true,
        description: 'The array field to filter',
      },
      {
        key: 'field',
        label: 'Item Field',
        type: 'text',
        placeholder: 'e.g., status, type',
        required: true,
        description: 'The field within each item to check',
      },
      {
        key: 'operator',
        label: 'Operator',
        type: 'select',
        defaultValue: 'equals',
        options: [
          { value: 'equals', label: 'Equals' },
          { value: 'notEquals', label: 'Not Equals' },
          { value: 'contains', label: 'Contains' },
          { value: 'greaterThan', label: 'Greater Than' },
          { value: 'lessThan', label: 'Less Than' },
          { value: 'isEmpty', label: 'Is Empty' },
          { value: 'isNotEmpty', label: 'Is Not Empty' },
        ],
        description: 'Comparison operator',
      },
      {
        key: 'value',
        label: 'Value',
        type: 'text',
        placeholder: 'Value to match',
        description: 'The value to compare with',
      },
    ],
  },

  'transform-map': {
    type: 'transform-map',
    category: 'transform',
    label: 'Map / Transform',
    description: 'Transform data structure or values',
    icon: 'Shuffle',
    color: '#8b5cf6',
    defaultConfig: {
      mappings: [],
    },
    configSchema: [
      {
        key: 'inputField',
        label: 'Input Field',
        type: 'text',
        placeholder: 'e.g., data, response.body',
        description: 'The input data field to transform',
      },
      {
        key: 'outputField',
        label: 'Output Field',
        type: 'text',
        placeholder: 'e.g., transformedData',
        description: 'Where to store the transformed data',
      },
      {
        key: 'mappings',
        label: 'Field Mappings',
        type: 'json',
        placeholder: '[{ "from": "oldField", "to": "newField" }]',
        defaultValue: [],
        description: 'Array of field mapping definitions',
      },
      {
        key: 'expression',
        label: 'Transform Expression',
        type: 'textarea',
        placeholder: 'JavaScript expression, e.g., item.value * 2',
        description: 'Custom transformation expression',
      },
    ],
  },

  'transform-aggregate': {
    type: 'transform-aggregate',
    category: 'transform',
    label: 'Aggregate',
    description: 'Combine multiple items into summary',
    icon: 'Layers',
    color: '#8b5cf6',
    defaultConfig: {
      operation: 'count',
      field: '',
    },
    configSchema: [
      {
        key: 'arrayField',
        label: 'Array to Aggregate',
        type: 'text',
        placeholder: 'e.g., data.items',
        required: true,
        description: 'The array field to aggregate',
      },
      {
        key: 'operation',
        label: 'Operation',
        type: 'select',
        defaultValue: 'count',
        options: [
          { value: 'count', label: 'Count' },
          { value: 'sum', label: 'Sum' },
          { value: 'average', label: 'Average' },
          { value: 'min', label: 'Minimum' },
          { value: 'max', label: 'Maximum' },
          { value: 'concat', label: 'Concatenate' },
          { value: 'unique', label: 'Unique Values' },
          { value: 'groupBy', label: 'Group By' },
        ],
        description: 'Aggregation operation to perform',
      },
      {
        key: 'field',
        label: 'Field',
        type: 'text',
        placeholder: 'e.g., amount, value',
        description: 'The field to aggregate (for sum, avg, etc.)',
      },
      {
        key: 'groupByField',
        label: 'Group By Field',
        type: 'text',
        placeholder: 'e.g., category, type',
        description: 'Field to group by (when using Group By)',
      },
      {
        key: 'outputField',
        label: 'Output Field',
        type: 'text',
        placeholder: 'e.g., result, summary',
        defaultValue: 'aggregateResult',
        description: 'Where to store the aggregation result',
      },
    ],
  },
}

/**
 * Get node definitions grouped by category
 */
export function getNodesByCategory(): Record<string, NodeDefinition[]> {
  const grouped: Record<string, NodeDefinition[]> = {
    trigger: [],
    action: [],
    logic: [],
    transform: [],
  }

  Object.values(NODE_DEFINITIONS).forEach((definition) => {
    if (grouped[definition.category]) {
      grouped[definition.category].push(definition)
    }
  })

  return grouped
}

/**
 * Get a single node definition by type
 */
export function getNodeDefinition(type: WorkflowNodeType): NodeDefinition | undefined {
  return NODE_DEFINITIONS[type]
}

/**
 * Category metadata for display
 */
export const CATEGORY_INFO: Record<NodeCategory, { label: string; description: string; icon: string; color: string }> = {
  trigger: {
    label: 'Triggers',
    description: 'Events that start the workflow',
    icon: 'Play',
    color: '#22c55e',
  },
  action: {
    label: 'Actions',
    description: 'Operations that perform tasks',
    icon: 'Zap',
    color: '#3b82f6',
  },
  logic: {
    label: 'Logic',
    description: 'Control the flow of execution',
    icon: 'GitBranch',
    color: '#f59e0b',
  },
  transform: {
    label: 'Transform',
    description: 'Manipulate and transform data',
    icon: 'Shuffle',
    color: '#8b5cf6',
  },
  integration: {
    label: 'Integration',
    description: 'Connect with external services',
    icon: 'Globe',
    color: '#ec4899',
  },
}
