import { defineModel, Model } from "react-class-model";

export const [FormModelProvider, useFormModel, watchFormModel] =
    defineModel<FormModel>();

type FieldValue = string | (() => string) | (() => Promise<string>);

export class FormModel extends Model {
    private readonly fields: Map<string, FieldValue> = new Map();

    public addField(key: string, value: FieldValue) {
        this.fields.set(key, value);
    }

    public clearFields() {
        this.fields.clear();
    }

    public async capture(): Promise<Map<string, string>> {
        const data = new Map<string, string>();
        for (const [key, value] of this.fields.entries()) {
            if (typeof value === "function") {
                const result = value();
                data.set(key, result instanceof Promise ? await result : result);
            } else {
                data.set(key, value);
            }
        }
        return data;
    }
}
