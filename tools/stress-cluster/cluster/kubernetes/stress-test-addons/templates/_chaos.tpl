{{- define "stress-test-addons.chaos-wrapper.tpl" -}}
{{- $global := index . 0 -}}
{{- $chaosTemplate := index . 1 -}}
{{- $scenariosOverride := $global.Values.scenarios -}}
{{- if eq (len .) 3 -}}
{{- $scenariosOverride = index . 2 -}}
{{- end -}}
{{- range (default (list "stress") $scenariosOverride) }}
---
{{ $chaosCtx := fromYaml (include "stress-test-addons.util.mergeStressContext" (list $global . )) }}
metadata:
  name: "{{ lower $chaosCtx.Stress.Scenario }}-{{ $chaosCtx.Release.Name }}-{{ $chaosCtx.Release.Revision }}"
  namespace: {{ $chaosCtx.Release.Namespace }}
  annotations:
    'experiment.chaos-mesh.org/pause': 'true'
{{ include $chaosTemplate $chaosCtx }}
{{- end -}}
{{- end -}}
