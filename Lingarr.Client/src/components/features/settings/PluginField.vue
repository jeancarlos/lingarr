<template>
    <div>
        <template v-if="field.type === PLUGIN_SETTING_TYPE.REMOTE_DROPDOWN">
            <label class="mb-1 block text-sm font-semibold" :for="field.key">
                {{ field.label }}
                <span v-if="field.required" class="text-red-500" aria-hidden="true">*</span>
            </label>
            <SelectComponent
                ref="remoteSelect"
                :options="remoteOptions"
                :selected="fieldValue"
                :load-on-open="true"
                placeholder="Select option..."
                :no-options="remoteError ?? 'Loading options...'"
                @update:selected="(value: string) => (fieldValue = value)"
                @fetch-options="loadRemoteOptions" />
        </template>
        <template v-else-if="field.type === PLUGIN_SETTING_TYPE.KEY_VALUE_LIST">
            <label class="mb-1 block text-sm font-semibold" :for="field.key">
                {{ field.label }}
                <span v-if="field.required" class="text-red-500" aria-hidden="true">*</span>
            </label>
            <ol :id="field.key" class="space-y-2">
                <li
                    v-for="(pair, index) in pairs"
                    :key="`pair-${index}`"
                    class="flex items-center gap-3 rounded-md border border-accent/30 p-3">
                    <div class="min-w-0 flex-1">
                        <InputComponent
                            v-model="pair.name"
                            size="sm"
                            placeholder="Name"
                            @update:model-value="writePairs" />
                    </div>
                    <div class="min-w-0 flex-1">
                        <InputComponent
                            v-model="pair.value"
                            size="sm"
                            placeholder="Value"
                            @update:model-value="writePairs" />
                    </div>
                    <button
                        type="button"
                        class="text-primary-content hover:text-primary-content/50 focus-visible:ring-accent cursor-pointer rounded p-1 transition-colors focus-visible:ring-2 focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-30"
                        title="Remove header"
                        aria-label="Remove header"
                        @click="removePair(index)">
                        <TrashIcon class="h-4 w-4" />
                    </button>
                </li>
                <li>
                    <ButtonComponent variant="ghost" size="xs" @click="addPair">
                        <PlusIcon class="mr-1 h-3 w-3" />
                        Add header
                    </ButtonComponent>
                </li>
            </ol>
        </template>
        <InputComponent
            v-else
            :id="field.key"
            v-model="fieldValue"
            :type="field.type === PLUGIN_SETTING_TYPE.SECRET ? INPUT_TYPE.PASSWORD : INPUT_TYPE.TEXT"
            :label="field.label"
            :placeholder="field.default ?? ''"
            :validation-type="validationType"
            :min-length="field.minLength ?? undefined"
            :error-message="field.validationErrorMessage ?? undefined"
            @update:validation="(value: boolean) => (isValid = value)" />
        <div v-if="field.description" class="mt-1 text-xs opacity-60">
            {{ field.description }}
        </div>
    </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import {
    IEncryptedSettings,
    INPUT_TYPE,
    INPUT_VALIDATION_TYPE,
    InputValidationType,
    IPluginSettingField,
    ISettings,
    LabelValue,
    PLUGIN_SETTING_TYPE,
    SelectComponentExpose
} from '@/ts'
import services from '@/services'
import { useSettingStore } from '@/store/setting'
import InputComponent from '@/components/common/InputComponent.vue'
import SelectComponent from '@/components/common/SelectComponent.vue'
import ButtonComponent from '@/components/common/ButtonComponent.vue'
import PlusIcon from '@/components/icons/PlusIcon.vue'
import TrashIcon from '@/components/icons/TrashIcon.vue'

const props = defineProps<{
    field: IPluginSettingField
}>()

const emit = defineEmits(['save'])

const settingsStore = useSettingStore()

const isValid = ref<boolean>(
    props.field.minLength == null && props.field.type !== PLUGIN_SETTING_TYPE.URL
)

const remoteOptions = ref<LabelValue[]>([])
const remoteError = ref<string | null>(null)
const remoteSelect = ref<SelectComponentExpose | null>(null)

const validationType = computed<InputValidationType | undefined>(() => {
    if (props.field.type === PLUGIN_SETTING_TYPE.URL) {
        return INPUT_VALIDATION_TYPE.URL
    }
    if (props.field.minLength != null) {
        return INPUT_VALIDATION_TYPE.STRING
    }
    return undefined
})

const fieldValue = computed<string>({
    get: (): string => {
        if (props.field.type === PLUGIN_SETTING_TYPE.SECRET) {
            const stored = settingsStore.getEncryptedSetting(
                props.field.key as keyof IEncryptedSettings
            )
            return typeof stored === 'string' ? stored : ''
        }
        const stored = settingsStore.getSetting(props.field.key as keyof ISettings)
        if (typeof stored === 'string') {
            return stored
        }
        if (typeof stored === 'number' || typeof stored === 'boolean') {
            return String(stored)
        }
        return props.field.default ?? ''
    },
    set: (newValue: string): void => {
        if (props.field.type === PLUGIN_SETTING_TYPE.SECRET) {
            settingsStore.updateEncryptedSetting(
                props.field.key as keyof IEncryptedSettings,
                newValue,
                isValid.value
            )
        } else {
            settingsStore.updateSetting(
                props.field.key as keyof ISettings,
                newValue,
                isValid.value
            )
        }
        if (isValid.value) {
            emit('save')
        }
    }
})

const pairs = ref<{ name: string; value: string }[]>(readPairs())

// The store fills in asynchronously, so pick up a stored value that arrives after mount.
watch(
    () => fieldValue.value,
    (value: string) => {
        if (value !== serialisePairs()) {
            pairs.value = readPairs()
        }
    }
)

function readPairs(): { name: string; value: string }[] {
    return fieldValue.value
        .split(/[\r\n]+/)
        .map((line: string) => line.trim())
        .filter((line: string) => line.length > 0 && !line.startsWith('#'))
        .map((line: string) => {
            const separator = line.indexOf(':')
            if (separator <= 0) {
                return { name: line, value: '' }
            }
            return {
                name: line.slice(0, separator).trim(),
                value: line.slice(separator + 1).trim()
            }
        })
}

function serialisePairs(): string {
    return pairs.value
        .filter((pair) => pair.name.trim().length > 0)
        .map((pair) => `${pair.name.trim()}: ${pair.value.trim()}`)
        .join('\n')
}

function writePairs(): void {
    fieldValue.value = serialisePairs()
}

function addPair(): void {
    pairs.value.push({ name: '', value: '' })
}

function removePair(index: number): void {
    pairs.value.splice(index, 1)
    writePairs()
}

async function loadRemoteOptions(): Promise<void> {
    if (!props.field.optionsEndpoint) {
        remoteError.value = 'No options endpoint configured.'
        remoteSelect.value?.setLoadingState(false)
        return
    }

    try {
        remoteError.value = null
        const response = await services.plugin.getOptions(props.field.optionsEndpoint)
        remoteOptions.value = response.options ?? []
        if (remoteOptions.value.length === 0) {
            remoteError.value = response.message ?? 'No options returned.'
        }
    } catch (error) {
        console.error('Failed to load options:', error)
        remoteError.value = 'Error loading options. Please try again.'
    } finally {
        remoteSelect.value?.setLoadingState(false)
    }
}
</script>
