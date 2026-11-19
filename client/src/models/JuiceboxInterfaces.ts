export interface JuiceboxDisplay {
    readonly Header?: JuiceboxHeader;
    readonly Stage?: JuiceboxStage;
    readonly Form?: JuiceboxForm;
}

export interface JuiceboxHeader {
    readonly RoundNumber?: number;
    readonly RoundTime?: number;
}

export interface JuiceboxStage {
    readonly Title?: string;
    readonly Subtitle?: string;
}

export interface JuiceboxForm {
    readonly Header?: string;
    readonly SubmitLabel?: string;
    readonly Controls?: JuiceboxFormControl[];
}

export interface JuiceboxInput {
    readonly Type: "Input";
    readonly Key: string;
    readonly Label: string;
    readonly Placeholder: string;
    readonly MaxLength: number;
    readonly Value: string;
}

export interface JuiceboxTextarea {
    readonly Type: "Textarea";
    readonly Key: string;
    readonly Label: string;
    readonly Placeholder: string;
    readonly MaxLength: number;
    readonly Rows: number;
    readonly Value: string;
}

export interface JuiceboxRadioGroup {
    readonly Type: "RadioGroup";
    readonly Key: string;
    readonly Options: {
        readonly Label: string;
        readonly Value: string;
    }[];
}

export interface JuiceboxDrawing {
    readonly Type: "Drawing";
    readonly Key: string;
    readonly Width: number;
    readonly Height: number;
}

export interface JuiceboxButton {
    readonly Type: "Button";
    readonly Key: string;
    readonly Label: string;
    readonly Variant: string;
}

export type JuiceboxFormControl =
    | JuiceboxInput
    | JuiceboxTextarea
    | JuiceboxRadioGroup
    | JuiceboxDrawing
    | JuiceboxButton;
