// Monaco YAML editor component for workflow editing
import { useCallback, useRef, useEffect } from 'react'
import Editor, { type OnMount, type OnChange } from '@monaco-editor/react'
import type { editor } from 'monaco-editor'
import { useTheme } from '@/contexts/ThemeContext'

interface WorkflowYamlEditorProps {
  value: string
  onChange: (value: string) => void
  onValidationError?: (errors: string[]) => void
  readOnly?: boolean
  className?: string
}

export function WorkflowYamlEditor({
  value,
  onChange,
  onValidationError,
  readOnly = false,
  className,
}: WorkflowYamlEditorProps) {
  const editorRef = useRef<editor.IStandaloneCodeEditor | null>(null)
  const { theme } = useTheme()

  const handleEditorMount: OnMount = useCallback((editor) => {
    editorRef.current = editor

    // Configure editor options
    editor.updateOptions({
      minimap: { enabled: false },
      lineNumbers: 'on',
      scrollBeyondLastLine: false,
      wordWrap: 'on',
      tabSize: 2,
      insertSpaces: true,
      formatOnPaste: true,
      automaticLayout: true,
    })
  }, [])

  const handleChange: OnChange = useCallback(
    (newValue) => {
      if (newValue !== undefined) {
        onChange(newValue)

        // Basic YAML validation
        try {
          const errors: string[] = []

          // Check for tabs (YAML prefers spaces)
          if (newValue.includes('\t')) {
            errors.push('YAML should use spaces instead of tabs')
          }

          // Check for inconsistent indentation
          const lines = newValue.split('\n')
          for (let i = 0; i < lines.length; i++) {
            const line = lines[i]
            if (line.trim() && !line.trim().startsWith('#')) {
              const currentIndent = line.search(/\S/)
              if (currentIndent > 0 && currentIndent % 2 !== 0) {
                errors.push(`Line ${i + 1}: Indentation should be a multiple of 2 spaces`)
              }
            }
          }

          onValidationError?.(errors)
        } catch (e) {
          onValidationError?.([String(e)])
        }
      }
    },
    [onChange, onValidationError]
  )

  // Update editor theme based on app theme
  useEffect(() => {
    if (editorRef.current) {
      editorRef.current.updateOptions({
        theme: theme === 'dark' ? 'vs-dark' : 'vs',
      })
    }
  }, [theme])

  return (
    <div className={className || 'w-full h-full'}>
      <Editor
        height="100%"
        language="yaml"
        value={value}
        onChange={handleChange}
        onMount={handleEditorMount}
        theme={theme === 'dark' ? 'vs-dark' : 'vs'}
        options={{
          readOnly,
          minimap: { enabled: false },
          lineNumbers: 'on',
          scrollBeyondLastLine: false,
          wordWrap: 'on',
          tabSize: 2,
          insertSpaces: true,
          formatOnPaste: true,
          automaticLayout: true,
          fontSize: 13,
          fontFamily: 'JetBrains Mono, Menlo, Monaco, Consolas, monospace',
        }}
      />
    </div>
  )
}

// Default YAML template for new workflows
export const defaultWorkflowYaml = `name: New Workflow
version: "1.0"
description: ""

triggers:
  - id: manual-trigger
    type: Manual
    isEnabled: true

steps:
  - id: step-1
    name: First Step
    type: Job
    config:
      command: echo "Hello World"
      agentGroup: default

variables: {}

timeout: 1h

errorHandling:
  strategy: StopOnFirstError
`
