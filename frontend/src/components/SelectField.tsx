import { useId, type SelectHTMLAttributes } from "react";

export function SelectField({
  label,
  fieldClassName = "field",
  ...props
}: SelectHTMLAttributes<HTMLSelectElement> & {
  label: string;
  fieldClassName?: string;
}) {
  const generatedId = useId();
  const id = props.id ?? generatedId;
  return (
    <div className={fieldClassName}>
      <label htmlFor={id}>{label}</label>
      <select {...props} id={id} />
    </div>
  );
}
